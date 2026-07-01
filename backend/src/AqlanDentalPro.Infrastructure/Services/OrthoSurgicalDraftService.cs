using System.Text;
using System.Text.Json;
using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Sprint A8 — AI text-drafting assistant for the shared Ortho-Surgical planning case.
/// Same safety machinery as <see cref="OrthoCaseDraftService"/> (which this mirrors
/// closely): Settings-gated (ai.ceph_draft_enabled et al via <see cref="CephAiDraftService"/>),
/// honest Arabic errors (403 disabled/unsupported/missing key, 429 monthly limit, 502
/// upstream), never auto-saved (the caller must explicitly copy the draft into
/// JointPlan/SurgeonReview), and every attempt logged to <c>OrthodonticAiLogs</c> for audit
/// per docs/ortho-module/ORTHO_SURGICAL_AI_VISION_EXPANSION.md §4 Phase A.
///
/// Sections: case_summary, referral_letter, patient_explanation, joint_plan_draft — the
/// four outputs called out for Sprint A8 in the plan ("تلخيص/إحالة/شرح مريض" /
/// "summary, referral draft, patient explanation, joint plan draft").
/// </summary>
public sealed class OrthoSurgicalDraftService(
    AppDbContext db,
    CephAiDraftService aiSettings,
    IEnumerable<IAiDraftProvider> providers,
    ICurrentUserService currentUser,
    AiApiKeyVault keyVault,
    ILogger<OrthoSurgicalDraftService> logger)
{
    public const string DisclaimerAr =
        "هذه مسودة مساعدة فقط — تتطلب مراجعة واعتماد أخصائي التقويم وأخصائي الجراحة قبل أي استخدام سريري. " +
        "لا تُعد قرارًا جراحيًا نهائيًا.";

    private static readonly HashSet<string> AllowedSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "case_summary", "referral_letter", "patient_explanation", "joint_plan_draft"
    };

    public async Task<OrthoCaseDraftResultDto?> GenerateAsync(Guid orthoSurgicalCaseId, string? section, CancellationToken ct = default)
    {
        var normalizedSection = NormalizeSection(section);
        if (normalizedSection is null)
            throw new ArgumentException("قسم المسودة غير مدعوم");

        var c = await db.OrthoSurgicalCases.AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Orthodontist)
            .Include(x => x.Surgeon)
            .Include(x => x.SurgeonReview)
            .Include(x => x.JointPlan)
            .FirstOrDefaultAsync(x => x.Id == orthoSurgicalCaseId && x.IsActive, ct);
        if (c is null) return null;

        var summary = await BuildCaseSummaryAsync(c, ct);
        var inputSummary = BuildInputSummary(summary, normalizedSection);
        var settings = await aiSettings.GetSettingsAsync();

        if (!settings.Enabled)
        {
            await WriteAuditAsync(orthoSurgicalCaseId, normalizedSection, null, false, "feature_disabled", inputSummary, 0, true, ct);
            throw new CephAiUnavailableException(CephAiDraftService.DisabledMessageAr);
        }

        var provider = providers.FirstOrDefault(p => string.Equals(p.ProviderName, settings.Provider, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            await WriteAuditAsync(orthoSurgicalCaseId, normalizedSection, null, false, $"provider_unsupported:{settings.Provider}", inputSummary, 0, true, ct);
            throw new CephAiUnavailableException(CephAiDraftService.UnsupportedProviderMessageAr(settings.Provider));
        }

        var apiKey = await keyVault.ResolveAsync(provider.ProviderName, provider.ApiKeyEnvVar, ct);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await WriteAuditAsync(orthoSurgicalCaseId, normalizedSection, null, false, "api_key_missing", inputSummary, 0, true, ct);
            throw new CephAiUnavailableException(CephAiDraftService.MissingKeyMessageAr);
        }

        var modelId = $"{provider.ProviderName}/{settings.Model}";
        if (settings.MonthlyLimit > 0 && await aiSettings.CountUsageThisMonthAsync() >= settings.MonthlyLimit)
        {
            await WriteAuditAsync(orthoSurgicalCaseId, normalizedSection, modelId, false, "monthly_limit_reached", inputSummary, 0, true, ct);
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
            await WriteAuditAsync(orthoSurgicalCaseId, normalizedSection, modelId, false, ex.Message, inputSummary, 0, true, ct);
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
            logger.LogError(ex, "[OrthoSurgicalDraft] provider call failed for case {CaseId}", orthoSurgicalCaseId);
            await WriteAuditAsync(orthoSurgicalCaseId, normalizedSection, modelId, false, reason, inputSummary, 0, true, ct);
            throw new CephAiUpstreamException(reason, ex);
        }

        await WriteAuditAsync(orthoSurgicalCaseId, normalizedSection, modelId, true, null, inputSummary, draft.Length, false, ct);

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

    // Structured, de-identified evidence only — no patient name/phone/whatsapp is ever
    // included, matching Phase A of §4: "لا ترسل أرقام هواتف أو بيانات شخصية عند عدم الحاجة".
    private async Task<CaseSummary> BuildCaseSummaryAsync(Domain.Entities.OrthoSurgicalCase c, CancellationToken ct)
    {
        var summary = new CaseSummary { Status = c.Status.ToString() };

        var diagnosis = await db.OrthoDiagnoses.AsNoTracking().FirstOrDefaultAsync(d => d.OrthoCaseId == c.OrthoCaseId, ct);
        if (diagnosis is null) summary.MissingData.Add("orthodontic diagnosis missing");
        else
        {
            summary.EvidenceUsed.Add("diagnosis");
            summary.Diagnosis = string.Join("; ", new[]
            {
                Pair("skeletal", diagnosis.SkeletalClassification), Pair("dental", diagnosis.DentalClassification),
                Pair("facial", diagnosis.FacialPattern), Pair("summary", diagnosis.Summary)
            }.Where(v => v is not null));
        }

        var ceph = c.CephAnalysisId.HasValue
            ? await db.CephAnalyses.AsNoTracking().Include(a => a.Measurements)
                .FirstOrDefaultAsync(a => a.Id == c.CephAnalysisId, ct)
            : await db.CephAnalyses.AsNoTracking().Include(a => a.Measurements)
                .Where(a => a.OrthoCaseId == c.OrthoCaseId && a.IsActive)
                .OrderByDescending(a => a.AnalysisDate)
                .FirstOrDefaultAsync(ct);
        if (ceph is null) summary.MissingData.Add("cephalometric analysis missing");
        else
        {
            summary.EvidenceUsed.Add("ceph");
            var measurements = ceph.Measurements.Where(m => m.IsActive && m.MeasurementValue.HasValue)
                .OrderBy(m => m.MeasurementName).Take(12)
                .Select(m => $"{m.MeasurementName}:{m.MeasurementValue!.Value}{m.Unit}");
            summary.Ceph = $"approved:{ceph.IsApproved}; measurements[{string.Join(", ", measurements)}]";
            if (!ceph.IsApproved) summary.Warnings.Add("ceph analysis is not approved yet");
        }

        var checklist = await db.RecordsChecklists.AsNoTracking().FirstOrDefaultAsync(r => r.OrthoCaseId == c.OrthoCaseId, ct);
        var recordsComplete = checklist is not null && checklist.ExtraoralFrontal && checklist.ExtraoralProfile
            && checklist.IntraoralFrontal && checklist.IntraoralRight && checklist.IntraoralLeft
            && checklist.Opg && checklist.LateralCeph && checklist.StudyModels;
        summary.RecordsStatus = checklist is null ? "not saved" : recordsComplete ? "complete" : "incomplete";
        if (!recordsComplete) summary.Warnings.Add("records checklist incomplete");

        if (c.SurgeonReview is not null)
        {
            summary.EvidenceUsed.Add("surgeonReview");
            summary.SurgeonReview = string.Join("; ", new[]
            {
                Pair("decision", c.SurgeonReview.Decision), Pair("proposedProcedure", c.SurgeonReview.ProposedProcedure),
                Pair("risks", c.SurgeonReview.Risks), Pair("notes", c.SurgeonReview.Notes)
            }.Where(v => v is not null));
        }
        else summary.MissingData.Add("surgeon review missing");

        if (c.JointPlan is not null)
        {
            summary.EvidenceUsed.Add("jointPlan");
            summary.JointPlan = string.Join("; ", new[]
            {
                Pair("procedureType", c.JointPlan.ProcedureType), Pair("timing", c.JointPlan.Timing),
                Pair("orthodonticObjectives", c.JointPlan.OrthodonticObjectives), Pair("surgicalObjectives", c.JointPlan.SurgicalObjectives),
                Pair("preSurgicalRequirements", c.JointPlan.PreSurgicalRequirements), Pair("postSurgicalPlan", c.JointPlan.PostSurgicalPlan),
                Pair("risks", c.JointPlan.Risks)
            }.Where(v => v is not null));
            if (c.JointPlan.LockedAt is not null) summary.Warnings.Add("joint plan is already locked (approved) — draft is for reference only");
        }
        else summary.MissingData.Add("joint plan not drafted yet");

        return summary;
    }

    // One combined system+task prompt per section — kept close to OrthoCaseDraftService's
    // style (concise instruction + explicit "do not fabricate / no identifiers" guardrails).
    private static string BuildSystemPrompt(string section) => section switch
    {
        "case_summary" =>
            "You are an orthognathic-surgery planning assistant. Write a concise clinical case summary combining " +
            "the orthodontic diagnosis, cephalometric findings, and surgical review status. Ground every clinical " +
            "claim in the provided evidence; never fabricate numbers. Do not include patient identifiers. " +
            "This is a draft that requires specialist review.",
        "referral_letter" =>
            "You are drafting a referral letter FROM the orthodontist TO the oral and maxillofacial surgeon. " +
            "Include: a short clinical summary, the orthodontic objective, what records are attached, and one " +
            "clear question for the surgeon to answer. Formal but concise Arabic. Do not include patient " +
            "identifiers (name/phone). Do not fabricate data not present in the evidence.",
        "patient_explanation" =>
            "Write a simple, reassuring Arabic explanation for the PATIENT (not a clinician) describing: what the " +
            "problem is, why surgery may be needed, what the orthodontist will do, what the surgeon will do, and " +
            "what to expect after surgery. Avoid technical jargon and numeric measurements. Do not fabricate " +
            "information not present in the evidence — if something is unknown, say the team will explain it " +
            "during the visit.",
        "joint_plan_draft" =>
            "Draft the joint treatment plan fields (procedure type, timing, orthodontic objectives, surgical " +
            "objectives, pre-surgical requirements, post-surgical plan, risks/limitations) as short labeled " +
            "paragraphs the orthodontist and surgeon can review and edit. Ground every claim in the evidence; " +
            "never invent a specific procedure, movement amount, or timeline not implied by the evidence.",
        _ => "You are a clinical drafting assistant. Produce a concise draft grounded only in the provided evidence."
    } + " Output Arabic. Section: " + section;

    private static string BuildUserPrompt(string section, CaseSummary summary)
    {
        var b = new StringBuilder();
        b.AppendLine($"Requested section: {section}");
        b.AppendLine($"Case status: {summary.Status}");
        b.AppendLine("Evidence summary without patient identifiers:");
        Add(b, "Diagnosis", summary.Diagnosis);
        Add(b, "Ceph", summary.Ceph);
        Add(b, "Records status", summary.RecordsStatus);
        Add(b, "Surgeon review", summary.SurgeonReview);
        Add(b, "Joint plan (current draft, if any)", summary.JointPlan);
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
            // Reuses OrthodonticAiLogs (no new table) — same CLIN-32 convention as
            // OrthoCaseDraftService: AnalysisId semantically holds this case's Id here, NOT a
            // CephAnalysis.Id. The Action prefix ("ortho_surgical_draft:") distinguishes it.
            db.OrthodonticAiLogs.Add(new Domain.Entities.OrthodonticAiLog
            {
                AnalysisId = caseId,
                UserId = currentUser.UserId,
                Action = $"ortho_surgical_draft:{section}",
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
    private static void Add(StringBuilder b, string title, string? value) { if (!string.IsNullOrWhiteSpace(value)) b.AppendLine($"- {title}: {value}"); }
    private static void AddList(StringBuilder b, string title, IReadOnlyCollection<string> values) { if (values.Count > 0) b.AppendLine($"- {title}: {string.Join(" | ", values)}"); }

    private sealed class CaseSummary
    {
        public string Status { get; set; } = string.Empty;
        public List<string> EvidenceUsed { get; } = [];
        public List<string> MissingData { get; } = [];
        public List<string> Warnings { get; } = [];
        public string? Diagnosis { get; set; }
        public string? Ceph { get; set; }
        public string? RecordsStatus { get; set; }
        public string? SurgeonReview { get; set; }
        public string? JointPlan { get; set; }
    }
}
