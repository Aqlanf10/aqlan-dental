using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>Effective AI assistant configuration (Settings rows + safe defaults + clamps).</summary>
public sealed record AiDraftSettings(
    bool Enabled,
    string Provider,
    string Model,
    int MaxTokens,
    double Temperature,
    int MonthlyLimit);

/// <summary>
/// Ceph batch C-D — LLM draft-diagnosis ORCHESTRATOR. The actual API calls
/// live in the IAiDraftProvider implementations (Gemini / Anthropic); this
/// service only reads configuration, enforces the monthly limit, builds the
/// prompts, audits every attempt, and maps failures to honest typed errors.
///
/// Safety contract (owner's hard rules):
/// 1. Never fake AI: a real provider response or an honest typed exception —
///    no synthetic fallback text.
/// 2. Every output is a DRAFT: the result always carries the Arabic disclaimer
///    and is NEVER auto-saved.
/// 3. Every call attempt is audited in OrthodonticAiLogs (success AND failure)
///    with a counts-only InputSummary (no patient identifiers), the
///    provider+model in ModelId (e.g. "gemini/gemini-3.5-flash"), and a short
///    ErrorSummary (no secrets, no exception dumps, never the full prompt).
/// 4. API keys are read ONLY from backend environment variables (hosting-agnostic) — never
///    stored in the database, never returned to clients, never logged.
/// </summary>
public class CephAiDraftService(
    AppDbContext db,
    IEnumerable<IAiDraftProvider> providers,
    ICurrentUserService currentUser,
    AiApiKeyVault keyVault,
    ILogger<CephAiDraftService> logger)
{
    // ── Settings keys (Settings table) ────────────────────────────────────────
    /// <summary>Feature flag — missing or anything other than "true"/"1" means DISABLED.</summary>
    public const string DraftEnabledSettingKey = "ai.ceph_draft_enabled";
    public const string ProviderSettingKey = "ai.provider";
    public const string ModelSettingKey = "ai.model";
    public const string MaxTokensSettingKey = "ai.max_tokens";
    public const string TemperatureSettingKey = "ai.temperature";
    /// <summary>0 = unlimited; &gt;0 = max successful calls per calendar month.</summary>
    public const string MonthlyLimitSettingKey = "ai.monthly_limit";

    // ── Defaults and clamps ───────────────────────────────────────────────────
    public const string DefaultProvider = "gemini";
    public const string DefaultModelId = "gemini-3.5-flash";
    public const int DefaultMaxTokens = 1500;
    public const int MinMaxTokens = 100;
    public const int MaxMaxTokens = 8000;
    public const double DefaultTemperature = 0.4;
    public const int DefaultMonthlyLimit = 0;

    /// <summary>Providers the system recognizes — "openai" is recognized but not implemented yet.</summary>
    public static readonly IReadOnlyList<string> KnownProviders = ["gemini", "anthropic", "openai"];

    /// <summary>Named HttpClient (60s timeout) registered in ServiceRegistrationConfiguration.</summary>
    public const string HttpClientName = "CephAi";

    public const string ActionDraftDiagnosis = "ceph_draft_diagnosis";

    // ── Honest user-facing Arabic messages ────────────────────────────────────
    public const string DisclaimerAr =
        "هذه مسودة مولّدة بالذكاء الاصطناعي — تتطلب مراجعة واعتماد أخصائي التقويم قبل أي استخدام سريري.";

    public const string DisabledMessageAr = "مساعد الذكاء الاصطناعي معطل من الإعدادات";
    public const string MissingKeyMessageAr = "ميزة الذكاء الاصطناعي غير مفعلة أو لم يتم إعداد مفتاح API بعد.";
    public const string MonthlyLimitMessageAr = "تم بلوغ الحد الشهري لاستخدام الذكاء الاصطناعي";
    public const string UpstreamFailureMessageAr = "تعذر الاتصال بخدمة الذكاء الاصطناعي — حاول لاحقًا";

    /// <summary>Honest message for a recognized-but-unavailable provider (e.g. «مزود openai غير مدعوم بعد»).</summary>
    public static string UnsupportedProviderMessageAr(string provider) => $"مزود {provider} غير مدعوم بعد";

    // ──────────────────────────────────────────────────────────────────────────
    //  CONFIGURATION  (Settings table → effective values)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Reads the ai.* Settings rows and applies defaults + clamps.</summary>
    public async Task<AiDraftSettings> GetSettingsAsync()
    {
        var rows = await db.Settings
            .Where(s => s.Key.StartsWith("ai."))
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        var flag = rows.GetValueOrDefault(DraftEnabledSettingKey);
        var enabled = string.Equals(flag, "true", StringComparison.OrdinalIgnoreCase) || flag == "1";

        var provider = rows.GetValueOrDefault(ProviderSettingKey);
        if (string.IsNullOrWhiteSpace(provider)) provider = DefaultProvider;
        provider = provider.Trim().ToLowerInvariant();

        var model = rows.GetValueOrDefault(ModelSettingKey);
        if (string.IsNullOrWhiteSpace(model)) model = DefaultModelId;

        var maxTokens = ParseInt(rows.GetValueOrDefault(MaxTokensSettingKey), DefaultMaxTokens);
        maxTokens = Math.Clamp(maxTokens, MinMaxTokens, MaxMaxTokens);

        var temperature = ParseDouble(rows.GetValueOrDefault(TemperatureSettingKey), DefaultTemperature);
        temperature = Math.Clamp(temperature, 0d, 1d);

        var monthlyLimit = ParseInt(rows.GetValueOrDefault(MonthlyLimitSettingKey), DefaultMonthlyLimit);
        if (monthlyLimit < 0) monthlyLimit = 0;

        return new AiDraftSettings(enabled, provider, model!, maxTokens, temperature, monthlyLimit);
    }

    private static int ParseInt(string? raw, int fallback) =>
        int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    private static double ParseDouble(string? raw, double fallback) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    /// <summary>Successful AI calls in the current UTC calendar month (for the monthly limit + admin screen).</summary>
    public async Task<int> CountUsageThisMonthAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return await db.OrthodonticAiLogs.CountAsync(l => l.Succeeded && l.CreatedAt >= monthStart);
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  AVAILABILITY  (honest, distinguishable states)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Enabled = feature flag on AND provider implemented AND its env-var API
    /// key non-empty. When disabled, Error carries the honest Arabic reason.
    /// </summary>
    public async Task<(bool Enabled, string? Error)> GetAvailabilityAsync()
    {
        var settings = await GetSettingsAsync();
        if (!settings.Enabled) return (false, DisabledMessageAr);

        var provider = ResolveProvider(settings.Provider);
        if (provider is null) return (false, UnsupportedProviderMessageAr(settings.Provider));

        if (string.IsNullOrWhiteSpace(
                await keyVault.ResolveAsync(provider.ProviderName, provider.ApiKeyEnvVar)))
            return (false, MissingKeyMessageAr);

        return (true, null);
    }

    public async Task<bool> IsEnabledAsync() => (await GetAvailabilityAsync()).Enabled;

    private IAiDraftProvider? ResolveProvider(string providerName) =>
        providers.FirstOrDefault(p =>
            string.Equals(p.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

    // ──────────────────────────────────────────────────────────────────────────
    //  GENERATE DRAFT
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a real LLM draft diagnosis for the analysis via the configured
    /// provider. Returns null when the analysis does not exist (controller →
    /// Arabic 404). Throws CephAiUnavailableException when disabled/unsupported/
    /// unconfigured (→ 403), CephAiLimitReachedException when the monthly limit
    /// is reached (→ 429), and CephAiUpstreamException when the API call failed
    /// (→ Arabic 502). An OrthodonticAiLog audit row is written for every attempt.
    /// </summary>
    public async Task<CephAiDraftResultDto?> GenerateDraftAsync(Guid analysisId, CancellationToken ct = default)
    {
        var analysis = await db.CephAnalyses
            .Include(a => a.Measurements)
            .Include(a => a.Diagnosis)
            .FirstOrDefaultAsync(a => a.Id == analysisId, ct);

        if (analysis is null) return null; // controller maps to the Arabic 404

        var measurements = analysis.Measurements.Where(m => m.IsActive).ToList();
        var diagnosis = analysis.Diagnosis;

        // Optional, cheap: the latest clinical exam of the case adds context
        // (overjet/overbite/relations) — still zero patient identifiers.
        var exam = await db.OrthoClinicalExams
            .Where(e => e.OrthoCaseId == analysis.OrthoCaseId)
            .OrderByDescending(e => e.ExamDate)
            .FirstOrDefaultAsync(ct);

        var inputSummary =
            $"measurements:{measurements.Count};" +
            $"diagnosisPresent:{(diagnosis is not null ? "true" : "false")};" +
            $"examPresent:{(exam is not null ? "true" : "false")}";

        var settings = await GetSettingsAsync();

        if (!settings.Enabled)
        {
            // Audit every call — including honest refusals to run.
            await WriteAuditAsync(analysisId, modelId: null, succeeded: false,
                errorSummary: "feature_disabled", inputSummary, outputLength: 0, bestEffort: true);
            throw new CephAiUnavailableException(DisabledMessageAr);
        }

        var provider = ResolveProvider(settings.Provider);
        if (provider is null)
        {
            // "openai" (and any unknown value) — recognized but not implemented:
            // honest Arabic refusal, never a fake response.
            await WriteAuditAsync(analysisId, modelId: null, succeeded: false,
                errorSummary: $"provider_unsupported:{settings.Provider}", inputSummary, outputLength: 0, bestEffort: true);
            throw new CephAiUnavailableException(UnsupportedProviderMessageAr(settings.Provider));
        }

        // The key is resolved from an encrypted admin override first, then
        // from the provider environment variable. It is never returned/logged.
        var apiKey = await keyVault.ResolveAsync(provider.ProviderName, provider.ApiKeyEnvVar, ct);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await WriteAuditAsync(analysisId, modelId: null, succeeded: false,
                errorSummary: "api_key_missing", inputSummary, outputLength: 0, bestEffort: true);
            throw new CephAiUnavailableException(MissingKeyMessageAr);
        }

        var modelId = $"{provider.ProviderName}/{settings.Model}";

        if (settings.MonthlyLimit > 0 && await CountUsageThisMonthAsync() >= settings.MonthlyLimit)
        {
            await WriteAuditAsync(analysisId, modelId, succeeded: false,
                errorSummary: "monthly_limit_reached", inputSummary, outputLength: 0, bestEffort: true);
            throw new CephAiLimitReachedException(MonthlyLimitMessageAr);
        }

        var systemPrompt = BuildSystemPrompt();
        var userContent = BuildUserContent(analysis.AnalysisType, measurements, diagnosis, exam);

        string draftText;
        try
        {
            draftText = await provider.GenerateAsync(
                systemPrompt, userContent, settings.Model,
                settings.MaxTokens, settings.Temperature, apiKey, ct);
        }
        catch (CephAiUpstreamException ex)
        {
            // Provider already produced a short, secret-free reason.
            await WriteAuditAsync(analysisId, modelId, succeeded: false,
                errorSummary: ex.Message, inputSummary, outputLength: 0, bestEffort: true);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Short reason only — never the exception dump, never the API key.
            var reason = ex switch
            {
                TaskCanceledException => "timeout contacting AI API",
                JsonException => "unparseable AI API response",
                _ => "network error contacting AI API",
            };
            logger.LogError(ex, "[CephAiDraft] {Provider} API call failed for analysis {AnalysisId}",
                provider.ProviderName, analysisId);
            await WriteAuditAsync(analysisId, modelId, succeeded: false,
                errorSummary: reason, inputSummary, outputLength: 0, bestEffort: true);
            throw new CephAiUpstreamException(reason, ex);
        }

        // CEPH-TASK-001 (Codex P2 on #641): system-prompt rule 6 ASKS the model
        // to end the draft with DisclaimerAr, but a prompt is a request, not a
        // guarantee. Enforce it server-side so the draft BODY always carries the
        // disclaimer even when the doctor copies only the text — the separate
        // DTO Disclaimer field alone doesn't survive a copy of the body.
        if (!draftText.Contains(DisclaimerAr, StringComparison.Ordinal))
            draftText = draftText.TrimEnd() + "\n\n" + DisclaimerAr;

        await WriteAuditAsync(analysisId, modelId, succeeded: true,
            errorSummary: null, inputSummary, outputLength: draftText.Length);

        return new CephAiDraftResultDto
        {
            Draft = draftText,
            ModelId = modelId,
            Disclaimer = DisclaimerAr,
            GeneratedAt = DateTime.UtcNow,
        };
    }

    // bestEffort = true ONLY on refusal/error paths: an audit-write failure
    // there (e.g. missing OrthodonticAiLogs table on an un-migrated DB) must
    // never mask the honest AI error or turn an expected 403/429 into a 500.
    // The SUCCESS path stays strict (bestEffort = false): the monthly limit is
    // derived from successful audit rows, so a successful generation that cannot
    // be recorded must fail rather than silently bypass the limit / audit.
    private async Task WriteAuditAsync(
        Guid analysisId, string? modelId, bool succeeded,
        string? errorSummary, string? inputSummary, int outputLength,
        bool bestEffort = false)
    {
        try
        {
            db.OrthodonticAiLogs.Add(new OrthodonticAiLog
            {
                AnalysisId = analysisId,
                UserId = currentUser.UserId,
                Action = ActionDraftDiagnosis,
                ModelId = modelId,
                Succeeded = succeeded,
                ErrorSummary = errorSummary,
                InputSummary = inputSummary,
                OutputLength = outputLength,
            });
            await db.SaveChangesAsync();
        }
        catch (Exception ex) when (bestEffort)
        {
            logger.LogWarning(ex,
                "Failed to write OrthodonticAiLog audit row for analysis {AnalysisId} — continuing", analysisId);
            db.ChangeTracker.Clear();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    //  PROMPT BUILDING  (public static so tests can verify the safety wording)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// System role: Arabic clinical draft, structured, conservative, flags
    /// uncertainty, NEVER invents measurement values, ends with the review
    /// disclaimer.
    /// </summary>
    public static string BuildSystemPrompt() =>
        "أنت مساعد لأخصائي تقويم الأسنان في كتابة مسودة تشخيص سيفالومتري. التزم بالقواعد التالية بدقة:\n" +
        "1. اكتب مسودة سريرية بالعربية الفصحى المهنية.\n" +
        "2. نظّم المسودة في أقسام واضحة: التشخيص الهيكلي، التشخيص العمودي، التشخيص السني، الأهداف العلاجية، خيارات الخطة مع المفاضلة بينها، فحوصات إضافية مقترحة.\n" +
        "3. كن متحفظًا في الاستنتاجات وأشر صراحةً إلى أي نقطة غير مؤكدة أو تحتاج تأكيدًا سريريًا.\n" +
        "4. لا تخترع أي قيم قياس أو معطيات غير واردة في المدخلات إطلاقًا — اعتمد فقط على القياسات والملخصات المزوّدة.\n" +
        "5. لا تقدّم قرارًا علاجيًا نهائيًا؛ هذه مسودة للمراجعة فقط.\n" +
        "6. اختم المسودة حرفيًا بالعبارة: «" + DisclaimerAr + "»";

    /// <summary>
    /// User content: measurement table (name, value, normal ± SD,
    /// classification), the rule-engine summary, and anonymous clinical-exam
    /// highlights. Contains NO patient identifiers by design.
    /// </summary>
    public static string BuildUserContent(
        string? analysisType,
        IReadOnlyCollection<CephMeasurement> measurements,
        CephDiagnosis? diagnosis,
        OrthoClinicalExam? exam)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"نوع التحليل السيفالومتري: {(string.IsNullOrWhiteSpace(analysisType) ? "شامل" : analysisType)}");
        sb.AppendLine();
        sb.AppendLine("جدول القياسات (الاسم: القيمة — المعدل ± الانحراف المعياري — التصنيف):");
        if (measurements.Count == 0)
        {
            sb.AppendLine("- لا توجد قياسات محسوبة بعد.");
        }
        else
        {
            foreach (var m in measurements)
            {
                var value = m.MeasurementValue.HasValue ? $"{m.MeasurementValue.Value:0.#}{m.Unit}" : "—";
                var normal = m.NormalValue.HasValue
                    ? $"{m.NormalValue.Value:0.#}{m.Unit} ± {m.StdDeviation ?? 0:0.#}"
                    : "—";
                sb.AppendLine($"- {m.MeasurementName}: {value} (المعدل {normal}) — {m.Classification ?? "غير مصنف"}");
            }
        }

        if (diagnosis is not null)
        {
            sb.AppendLine();
            sb.AppendLine("ملخص محرك القواعد الآلي (rule engine):");
            if (!string.IsNullOrWhiteSpace(diagnosis.SkeletalClass))
                sb.AppendLine($"- الصنف الهيكلي: {diagnosis.SkeletalClass}");
            if (!string.IsNullOrWhiteSpace(diagnosis.VerticalPattern))
                sb.AppendLine($"- النمط الرأسي: {diagnosis.VerticalPattern}");
            if (!string.IsNullOrWhiteSpace(diagnosis.IncisorInclination))
                sb.AppendLine($"- ميلان القواطع: {diagnosis.IncisorInclination}");
            if (!string.IsNullOrWhiteSpace(diagnosis.AiRecommendation))
                sb.AppendLine(diagnosis.AiRecommendation);
        }

        if (exam is not null)
        {
            sb.AppendLine();
            sb.AppendLine("أبرز معطيات الفحص السريري (بدون أي مُعرّفات للمريض):");
            if (!string.IsNullOrWhiteSpace(exam.Profile)) sb.AppendLine($"- الملامح الجانبية: {exam.Profile}");
            if (!string.IsNullOrWhiteSpace(exam.MolarRelation)) sb.AppendLine($"- علاقة الأرحاء: {exam.MolarRelation}");
            if (!string.IsNullOrWhiteSpace(exam.CanineRelation)) sb.AppendLine($"- علاقة الأنياب: {exam.CanineRelation}");
            if (!string.IsNullOrWhiteSpace(exam.IncisorRelation)) sb.AppendLine($"- علاقة القواطع: {exam.IncisorRelation}");
            if (exam.Overjet.HasValue) sb.AppendLine($"- البروز الأفقي (Overjet): {exam.Overjet.Value:0.#} مم");
            if (exam.Overbite.HasValue) sb.AppendLine($"- العضة العميقة (Overbite): {exam.Overbite.Value:0.#} مم");
            if (exam.Crossbite) sb.AppendLine("- توجد عضة معكوسة");
            if (exam.OpenBite) sb.AppendLine("- توجد عضة مفتوحة");
            if (!string.IsNullOrWhiteSpace(exam.UpperCrowding)) sb.AppendLine($"- ازدحام علوي: {exam.UpperCrowding}");
            if (!string.IsNullOrWhiteSpace(exam.LowerCrowding)) sb.AppendLine($"- ازدحام سفلي: {exam.LowerCrowding}");
            if (!string.IsNullOrWhiteSpace(exam.Habits)) sb.AppendLine($"- عادات فموية: {exam.Habits}");
        }

        sb.AppendLine();
        sb.AppendLine("اكتب مسودة التشخيص الآن وفق التعليمات أعلاه دون اختراع أي قيمة غير واردة.");
        return sb.ToString();
    }
}
