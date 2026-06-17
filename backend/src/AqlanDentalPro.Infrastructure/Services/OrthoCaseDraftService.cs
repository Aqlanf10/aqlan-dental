using System.Text;
using System.Text.Json;
using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

public sealed class OrthoCaseDraftService(
    AppDbContext db,
    CephAiDraftService aiSettings,
    IEnumerable<IAiDraftProvider> providers,
    ICurrentUserService currentUser,
    AiApiKeyVault keyVault,
    ILogger<OrthoCaseDraftService> logger)
{
    public const string DisclaimerAr = "هذه مسودة مساعدة فقط — تتطلب مراجعة واعتماد أخصائي التقويم قبل أي استخدام سريري.";
    private static readonly HashSet<string> AllowedSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "problems", "diagnosis", "objectives", "strategies", "plan", "mechanotherapy"
    };

    public async Task<OrthoCaseDraftResultDto?> GenerateAsync(Guid caseId, string? section, CancellationToken ct = default)
    {
        var normalizedSection = NormalizeSection(section);
        if (normalizedSection is null)
            throw new ArgumentException("قسم المسودة غير مدعوم");

        var exists = await db.OrthoCases.AsNoTracking().AnyAsync(c => c.Id == caseId && c.IsActive, ct);
        if (!exists) return null;

        var summary = await BuildCaseSummaryAsync(caseId, ct);
        var inputSummary = BuildInputSummary(summary, normalizedSection);
        var settings = await aiSettings.GetSettingsAsync();

        if (!settings.Enabled)
        {
            await WriteAuditAsync(caseId, normalizedSection, null, false, "feature_disabled", inputSummary, 0, true, ct);
            throw new CephAiUnavailableException(CephAiDraftService.DisabledMessageAr);
        }

        var provider = providers.FirstOrDefault(p => string.Equals(p.ProviderName, settings.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            await WriteAuditAsync(caseId, normalizedSection, null, false, $"provider_unsupported:{settings.Provider}", inputSummary, 0, true, ct);
            throw new CephAiUnavailableException(CephAiDraftService.UnsupportedProviderMessageAr(settings.Provider));
        }

        var apiKey = await keyVault.ResolveAsync(provider.ProviderName, provider.ApiKeyEnvVar, ct);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await WriteAuditAsync(caseId, normalizedSection, null, false, "api_key_missing", inputSummary, 0, true, ct);
            throw new CephAiUnavailableException(CephAiDraftService.MissingKeyMessageAr);
        }

        var modelId = $"{provider.ProviderName}/{settings.Model}";
        if (settings.MonthlyLimit > 0 && await aiSettings.CountUsageThisMonthAsync() >= settings.MonthlyLimit)
        {
            await WriteAuditAsync(caseId, normalizedSection, modelId, false, "monthly_limit_reached", inputSummary, 0, true, ct);
            throw new CephAiLimitReachedException(CephAiDraftService.MonthlyLimitMessageAr);
        }

        string draft;
        try
        {
            draft = await provider.GenerateAsync(
                BuildSystemPrompt(normalizedSection),
                BuildUserPrompt(normalizedSection, summary),
                settings.Model,
                settings.MaxTokens,
                settings.Temperature,
                apiKey,
                ct);
        }
        catch (CephAiUpstreamException ex)
        {
            await WriteAuditAsync(caseId, normalizedSection, modelId, false, ex.Message, inputSummary, 0, true, ct);
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            var reason = ex switch
            {
                TaskCanceledException => "timeout contacting AI API",
                JsonException => "unparseable AI API response",
                _ => "network error contacting AI API"
            };
            logger.LogError(ex, "[OrthoCaseDraft] provider call failed for case {CaseId}", caseId);
            await WriteAuditAsync(caseId, normalizedSection, modelId, false, reason, inputSummary, 0, true, ct);
            throw new CephAiUpstreamException(reason, ex);
        }

        await WriteAuditAsync(caseId, normalizedSection, modelId, true, null, inputSummary, draft.Length, false, ct);

        return new OrthoCaseDraftResultDto
        {
            Section = normalizedSection,
            Draft = draft,
            EvidenceUsed = summary.EvidenceUsed,
            MissingData = summary.MissingData,
            Warnings = summary.Warnings,
            Disclaimer = DisclaimerAr,
            ModelId = modelId,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static string? NormalizeSection(string? section)
    {
        if (string.IsNullOrWhiteSpace(section)) return null;
        var value = section.Trim().ToLowerInvariant();
        return AllowedSections.Contains(value) ? value : null;
    }

    private async Task<CaseSummary> BuildCaseSummaryAsync(Guid caseId, CancellationToken ct)
    {
        var summary = new CaseSummary();

        var exam = await db.OrthoClinicalExams.AsNoTracking()
            .Where(e => e.OrthoCaseId == caseId)
            .OrderByDescending(e => e.ExamDate)
            .ThenByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (exam is null) summary.MissingData.Add("clinical examination missing");
        else
        {
            summary.EvidenceUsed.Add("clinicalExam");
            summary.ClinicalExam = string.Join("; ", new[]
            {
                Pair("profile", exam.Profile), Pair("facialSymmetry", exam.FacialSymmetry),
                Pair("molarRight", exam.MolarRelationRight ?? exam.MolarRelation), Pair("molarLeft", exam.MolarRelationLeft),
                Pair("canineRight", exam.CanineRelationRight ?? exam.CanineRelation), Pair("canineLeft", exam.CanineRelationLeft),
                Pair("overjet", exam.Overjet), Pair("overbite", exam.Overbite), Pair("upperCrowdingMm", exam.UpperCrowdingMm),
                Pair("lowerCrowdingMm", exam.LowerCrowdingMm), Pair("habits", exam.Habits), Pair("notes", exam.Notes)
            }.Where(v => !string.IsNullOrWhiteSpace(v)));
        }

        var problems = await db.ProblemListItems.AsNoTracking()
            .Where(p => p.OrthoCaseId == caseId && p.IsActive)
            .OrderBy(p => p.SortOrder)
            .Select(p => $"{p.Category}: {p.Description} ({p.Severity})")
            .ToListAsync(ct);
        if (problems.Count == 0) summary.MissingData.Add("problem list missing");
        else { summary.EvidenceUsed.Add("problemList"); summary.Problems = problems; }

        var diagnosis = await db.OrthoDiagnoses.AsNoTracking().FirstOrDefaultAsync(d => d.OrthoCaseId == caseId, ct);
        if (diagnosis is null) summary.MissingData.Add("orthodontic diagnosis missing");
        else
        {
            summary.EvidenceUsed.Add("diagnosis");
            summary.Diagnosis = string.Join("; ", new[]
            {
                Pair("skeletal", diagnosis.SkeletalClassification), Pair("dental", diagnosis.DentalClassification),
                Pair("facial", diagnosis.FacialPattern), Pair("softTissue", diagnosis.SoftTissueDiagnosis),
                Pair("functional", diagnosis.FunctionalDiagnosis), Pair("summary", diagnosis.Summary)
            }.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (diagnosis.ApprovedAt is null) summary.Warnings.Add("diagnosis is not approved yet");
        }

        var plan = await db.TreatmentPlans.AsNoTracking()
            .Where(p => p.OrthoCaseId == caseId && p.IsActive)
            .OrderByDescending(p => p.IsApproved)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (plan is null) summary.MissingData.Add("treatment plan missing");
        else
        {
            summary.EvidenceUsed.Add("treatmentPlan");
            summary.TreatmentPlan = string.Join("; ", new[]
            {
                Pair("appliance", plan.ApplianceType), Pair("bracketSystem", plan.BracketSystem), Pair("initialWire", plan.InitialWire),
                Pair("extraction", plan.ExtractionPlan), Pair("anchorage", plan.AnchoragePlan), Pair("goals", plan.TreatmentGoals),
                Pair("durationMonths", plan.ExpectedDurationMonths), Pair("retention", plan.RetentionPlan), Pair("risks", plan.RisksLimitations)
            }.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (!plan.IsApproved) summary.Warnings.Add("treatment plan is not approved yet");
        }

        var latestCeph = await db.CephAnalyses.AsNoTracking()
            .Include(c => c.Measurements)
            .Include(c => c.Diagnosis)
            .Where(c => c.OrthoCaseId == caseId && c.IsActive)
            // Same "latest" rule as the presentation deck generator
            // (OrthoCasePresentationService): AnalysisDate DESC, then CreatedAt
            // DESC — so the draft reasons over the same cephalometric analysis
            // the case presentation will render.
            .OrderByDescending(c => c.AnalysisDate)
            .ThenByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (latestCeph is null) summary.MissingData.Add("cephalometric analysis missing");
        else
        {
            summary.EvidenceUsed.Add("ceph");
            summary.Ceph = BuildCephEvidence(latestCeph, summary);
        }

        var model = await db.ModelAnalyses.AsNoTracking()
            .Where(m => m.OrthoCaseId == caseId && m.IsActive)
            .OrderByDescending(m => m.ApprovedAt != null)
            .ThenByDescending(m => m.AnalysisDate)
            .ThenByDescending(m => m.UpdatedAt)
            .FirstOrDefaultAsync(ct);
        if (model is null) summary.MissingData.Add("model analysis missing");
        else
        {
            summary.EvidenceUsed.Add("modelAnalysis");
            summary.ModelAnalysis = string.Join("; ", new[]
            {
                Pair("boltonOverall", model.BoltonOverall), Pair("boltonAnterior", model.BoltonAnterior),
                Pair("upperALD", model.UpperAld), Pair("lowerALD", model.LowerAld), Pair("pont", model.PontIndex), Pair("notes", model.Notes)
            }.Where(v => !string.IsNullOrWhiteSpace(v)));
            if (model.ApprovedAt is null) summary.Warnings.Add("model analysis is not approved yet");
        }

        var photoAnalysesCount = await db.PhotoAnalyses.AsNoTracking().CountAsync(p => p.OrthoCaseId == caseId, ct);
        if (photoAnalysesCount == 0) summary.MissingData.Add("photo analysis missing");
        else { summary.EvidenceUsed.Add("photoAnalysis"); summary.PhotoAnalysis = $"photoAnalyses:{photoAnalysesCount}"; }

        return summary;
    }

    // Rich cephalometric evidence: the analysis type, the ceph diagnosis
    // interpretation, and the actual measurement values (name:value unit) — so a
    // diagnosis/strategy draft is genuinely grounded in the cephalometric
    // findings instead of just knowing how many measurements exist.
    private static string BuildCephEvidence(Domain.Entities.CephAnalysis ceph, CaseSummary summary)
    {
        var parts = new List<string> { $"analysisType:{ceph.AnalysisType}" };

        var dx = ceph.Diagnosis;
        if (dx is not null)
        {
            var dxJoined = string.Join(", ", new[]
            {
                Pair("skeletal", dx.SkeletalClass), Pair("vertical", dx.VerticalPattern),
                Pair("incisors", dx.IncisorInclination), Pair("softTissue", dx.SoftTissueSummary),
                Pair("final", dx.FinalDiagnosis)
            }.Where(v => v is not null));
            if (!string.IsNullOrWhiteSpace(dxJoined)) parts.Add("cephDiagnosis{" + dxJoined + "}");
            if (!dx.DoctorApproved) summary.Warnings.Add("ceph diagnosis is not approved yet");
        }

        var measurements = ceph.Measurements
            .Where(m => m.IsActive && m.MeasurementValue.HasValue)
            .OrderBy(m => m.MeasurementName)
            .Take(16)
            .Select(m =>
            {
                var cls = !string.IsNullOrWhiteSpace(m.Classification) &&
                          !string.Equals(m.Classification, "normal", StringComparison.OrdinalIgnoreCase)
                    ? $"({m.Classification})" : string.Empty;
                return $"{m.MeasurementName}:{m.MeasurementValue!.Value}{m.Unit}{cls}";
            })
            .ToList();
        if (measurements.Count > 0) parts.Add("measurements[" + string.Join(", ", measurements) + "]");
        else summary.Warnings.Add("cephalometric measurements not computed");

        return string.Join("; ", parts);
    }

    private static string BuildSystemPrompt(string section) =>
        "You are an orthodontic clinical assistant. Produce a concise structured draft for the requested section only. " +
        "Ground skeletal, dental, and soft-tissue conclusions in the provided cephalometric measurements, ceph diagnosis, and model analysis; " +
        "cite the specific values you used and never fabricate numbers. " +
        "Do not invent missing data. Mention missing evidence when relevant. Do not include patient identifiers. " +
        "This is a draft that requires specialist review. Section: " + section;

    private static string BuildUserPrompt(string section, CaseSummary summary)
    {
        var b = new StringBuilder();
        b.AppendLine($"Requested section: {section}");
        b.AppendLine("Evidence summary without patient identifiers:");
        Add(b, "Clinical exam", summary.ClinicalExam);
        AddList(b, "Problem list", summary.Problems);
        Add(b, "Diagnosis", summary.Diagnosis);
        Add(b, "Treatment plan", summary.TreatmentPlan);
        Add(b, "Ceph", summary.Ceph);
        Add(b, "Model analysis", summary.ModelAnalysis);
        Add(b, "Photo analysis", summary.PhotoAnalysis);
        AddList(b, "Missing data", summary.MissingData);
        AddList(b, "Warnings", summary.Warnings);
        return b.ToString();
    }

    private static string BuildInputSummary(CaseSummary s, string section) =>
        $"section:{section};evidence:{s.EvidenceUsed.Count};missing:{s.MissingData.Count};warnings:{s.Warnings.Count}";

    private async Task WriteAuditAsync(Guid caseId, string section, string? modelId, bool succeeded, string? errorSummary, string inputSummary, int outputLength, bool bestEffort, CancellationToken ct)
    {
        try
        {
            db.OrthodonticAiLogs.Add(new Domain.Entities.OrthodonticAiLog
            {
                AnalysisId = caseId,
                UserId = currentUser.UserId,
                Action = $"ortho_case_clinical_draft:{section}",
                ModelId = modelId,
                Succeeded = succeeded,
                ErrorSummary = errorSummary,
                InputSummary = inputSummary,
                OutputLength = outputLength
            });
            await db.SaveChangesAsync(ct);
        }
        catch when (bestEffort) { }
    }

    private static string? Pair(string name, string? value) => string.IsNullOrWhiteSpace(value) ? null : $"{name}:{value.Trim()}";
    private static string? Pair(string name, decimal? value) => value.HasValue ? $"{name}:{value.Value}" : null;
    private static string? Pair(string name, int? value) => value.HasValue ? $"{name}:{value.Value}" : null;
    private static void Add(StringBuilder b, string title, string? value) { if (!string.IsNullOrWhiteSpace(value)) b.AppendLine($"- {title}: {value}"); }
    private static void AddList(StringBuilder b, string title, IReadOnlyCollection<string> values) { if (values.Count > 0) b.AppendLine($"- {title}: {string.Join(" | ", values)}"); }

    private sealed class CaseSummary
    {
        public List<string> EvidenceUsed { get; } = [];
        public List<string> MissingData { get; } = [];
        public List<string> Warnings { get; } = [];
        public List<string> Problems { get; set; } = [];
        public string? ClinicalExam { get; set; }
        public string? Diagnosis { get; set; }
        public string? TreatmentPlan { get; set; }
        public string? Ceph { get; set; }
        public string? ModelAnalysis { get; set; }
        public string? PhotoAnalysis { get; set; }
    }
}
