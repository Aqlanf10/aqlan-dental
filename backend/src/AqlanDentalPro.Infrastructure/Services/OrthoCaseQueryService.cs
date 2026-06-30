using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// READ-side query service for ortho cases.
///
/// Extracted from <c>OrthoCasesController</c> (2264 lines) following the
/// <c>LabOrderQueryService</c> pattern (PR #542): every GET / read endpoint's
/// EF query and projection moves here, while the controller keeps:
///   - permission checks (<c>OrthoAccess</c> policy + per-patient
///     <c>GetCaseAccessErrorAsync</c> / <c>DenyIfDoctorCannotAccess</c>),
///   - request DTO validation (e.g. category/phase enum normalization on
///     <c>GetPhotos</c>),
///   - all write/mutation endpoints (POST/PUT/PATCH/DELETE).
///
/// <para><b>Scope:</b> all GET endpoints under <c>/api/ortho-cases</c> whose
/// query logic was inline in the controller (the list/detail/visits/stages
/// reads already live in <c>OrthoService</c> — those are unchanged):</para>
/// <list type="bullet">
///   <item><c>GET /{id}/lab-orders</c> — lab orders linked to the case</item>
///   <item><c>GET /{id}/overview</c> — single-projection case overview</item>
///   <item><c>GET /{id}/clinical-exam</c> — latest clinical exam</item>
///   <item><c>GET /{id}/problem-list</c> — problem list items</item>
///   <item><c>GET /{id}/treatment-plans</c> — all plans (A/B/C)</item>
///   <item><c>GET /{id}/treatment-plan</c> — latest/approved plan</item>
///   <item><c>GET /{id}/extraction-decision</c> — latest extraction decision</item>
///   <item><c>GET /{id}/checklist</c> — saved checklist or auto-derived</item>
///   <item><c>GET /{id}/diagnosis</c> — saved diagnosis or computed summary</item>
///   <item><c>GET /{id}/retention</c> — retention record with visits</item>
///   <item><c>GET /{id}/retention/visits</c> — retention visits list</item>
///   <item><c>GET /{id}/photos</c> — clinical photos (filter + projection)</item>
///   <item><c>GET /{id}/photos/{photoId}/preparation</c> — image preparation</item>
/// </list>
///
/// <para><b>Not authorization-aware.</b> Controllers gate every action with
/// per-patient access checks BEFORE delegating here. The service only
/// executes EF queries and projects DTOs.</para>
///
/// <para><b>API contract unchanged.</b> DTO property names (PascalCase) match
/// the original anonymous-type projections exactly — System.Text.Json's
/// default camelCase policy serializes them to the same JSON keys, so the
/// response shape is identical (same fields, same null/non-null behavior,
/// same date string formats).</para>
///
/// <para><b>Behavior unchanged.</b> Same filters, same orderings, same
/// "latest row" subqueries, same auto-derive fallback for the checklist,
/// same computed-summary fallback for the diagnosis, same legacy
/// "unlinked ortho contract" fallback in the overview.</para>
/// </summary>
public class OrthoCaseQueryService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OrthoCaseQueryService> _logger;

    public OrthoCaseQueryService(AppDbContext db, ILogger<OrthoCaseQueryService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /api/ortho-cases/{id}/lab-orders ───────────────────────────────

    /// <summary>
    /// Read-only lab orders linked to this ortho case. Original controller
    /// returned <c>DateOnly?</c> values directly (no string conversion) — that
    /// is preserved (System.Text.Json serializes DateOnly as ISO date).
    /// </summary>
    public async Task<List<OrthoCaseLabOrderDto>> GetLabOrdersAsync(Guid id)
    {
        return await _db.LabOrders
            .AsNoTracking()
            .Where(o => o.OrthoCaseId == id && o.IsActive)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrthoCaseLabOrderDto
            {
                Id = o.Id,
                OrderNumber = o.OrderNumber,
                ApplianceType = o.ApplianceType,
                Status = o.Status,
                Priority = o.Priority,
                LabName = o.LabName,
                TotalCost = o.TotalCost,
                SentDate = o.SentDate,
                ExpectedDate = o.ExpectedDate,
                ReceivedDate = o.ReceivedDate,
                DeliveredDate = o.DeliveredDate,
            })
            .ToListAsync();
    }

    // ─── GET /api/ortho-cases/{id}/overview ─────────────────────────────────

    /// <summary>
    /// Single server-side projection for the overview DTO (CLIN-15). Returns
    /// <c>null</c> if the case does not exist (controller maps to 404 with the
    /// Arabic message). The contract lookup and optional photo projection for
    /// the auto-derive fallback run as additional round-trips — same as the
    /// original controller.
    /// </summary>
    public async Task<OrthoCaseOverviewDto?> GetOverviewAsync(Guid id)
    {
        var overview = await _db.OrthoCases
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.PatientId,
                TreatmentPlansCount = c.TreatmentPlans.Count,
                CompletedStages = c.Stages.Count(s => s.Status == "completed"),
                TotalStages = c.Stages.Count,
                VisitsCount = c.Visits.Count,
                PhotosCount = c.OrthoClinicalPhotos.Count,
                CephAnalysesCount = c.CephAnalyses.Count,
                PhotoAnalysesCount = c.PhotoAnalyses.Count,
                HasClinicalExam = c.ClinicalExam != null,
                ProblemsCount = c.ProblemList.Count,
                HasModelAnalysis = c.ModelAnalyses.Any(),
                LatestPlanIsApproved = c.TreatmentPlans
                    .OrderByDescending(p => p.CreatedAt)
                    .Select(p => p.IsApproved)
                    .FirstOrDefault(),
                LatestVisit = c.Visits
                    .OrderByDescending(v => v.VisitDate)
                    .Select(v => new { v.VisitDate, v.NextAppointmentDate })
                    .FirstOrDefault(),
                LatestCeph = c.CephAnalyses
                    .OrderByDescending(a => a.AnalysisDate)
                    .ThenByDescending(a => a.UpdatedAt)
                    .Select(a => new { a.Id, a.AnalysisDate })
                    .FirstOrDefault(),
                LatestProfile = c.PhotoAnalyses
                    .Where(a => a.ViewType == "profile")
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new { a.Id, a.CreatedAt })
                    .FirstOrDefault(),
                LatestFrontal = c.PhotoAnalyses
                    .Where(a => a.ViewType == "frontal")
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new { a.Id, a.CreatedAt })
                    .FirstOrDefault(),
                HasRetention = c.RetentionRecord != null,
                Diagnosis = c.Diagnosis == null ? null : new
                {
                    c.Diagnosis.ApprovedAt,
                    c.Diagnosis.CephSourceAnalysisId,
                    c.Diagnosis.CephSyncedAt,
                    c.Diagnosis.ProfileSourceAnalysisId,
                    c.Diagnosis.FrontalSourceAnalysisId,
                    c.Diagnosis.PhotoAnalysisSyncedAt,
                },
                Checklist = c.RecordsChecklist == null ? null : new
                {
                    c.RecordsChecklist.ExtraoralFrontal,
                    c.RecordsChecklist.ExtraoralProfile,
                    c.RecordsChecklist.ExtraoralSmile,
                    c.RecordsChecklist.IntraoralFrontal,
                    c.RecordsChecklist.IntraoralRight,
                    c.RecordsChecklist.IntraoralLeft,
                    c.RecordsChecklist.UpperOcclusal,
                    c.RecordsChecklist.LowerOcclusal,
                    c.RecordsChecklist.Opg,
                    c.RecordsChecklist.LateralCeph,
                    c.RecordsChecklist.Cbct,
                    c.RecordsChecklist.StudyModels,
                    c.RecordsChecklist.Consent,
                    c.RecordsChecklist.Contract,
                },
            })
            .FirstOrDefaultAsync();

        if (overview is null) return null;

        var hasDiagnosis = overview.Diagnosis is not null;
        var isDiagnosisApproved = overview.Diagnosis?.ApprovedAt is not null;

        // Contract selection: prefer contract explicitly linked to this ortho case.
        // Fallback: unlinked legacy ortho contracts (RelatedCaseId == null).
        // Never select a contract linked to a different ortho case.
        var contract = await _db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == overview.PatientId &&
                c.RelatedCaseId == id)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (contract is null)
        {
            // Legacy fallback: orthodontics contracts with no case link (pre-dating RelatedCaseId)
            var unlinkedOrthoContracts = await _db.Contracts
                .Include(c => c.Payments)
                .Where(c => c.PatientId == overview.PatientId &&
                    c.RelatedCaseId == null &&
                    (c.Specialty == "orthodontics" || c.Specialty == "ortho"))
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            // CLIN-30: When multiple unlinked legacy ortho contracts exist, pick the NEWEST one
            // (list is already ordered by CreatedAt descending) instead of silently nulling them all.
            // NOTE: this remains inherently ambiguous — the selected contract may not actually belong
            // to this ortho case, but hiding all contracts broke the overview for legacy patients.
            // Consistent with PatientJourneyController.LoadOrthoJourneySummariesAsync (newest-wins).
            contract = unlinkedOrthoContracts.FirstOrDefault();
        }

        decimal? contractTotal = null;
        decimal? contractPaid = null;
        decimal? contractRemaining = null;
        if (contract is not null)
        {
            contractTotal = contract.TotalAmount - contract.DiscountAmount;
            // FIX: Only count active payments — exclude soft-deleted/refunded payments
            contractPaid = contract.Payments.Where(p => p.IsActive).Sum(p => p.Amount);
            contractRemaining = Math.Max(0, contractTotal.Value - contractPaid.Value);
        }

        // Records checklist completion
        int checklistCompleted = 0;
        const int checklistTotal = 14;
        if (overview.Checklist is not null)
        {
            var checklist = overview.Checklist;
            checklistCompleted = new[]
            {
                checklist.ExtraoralFrontal, checklist.ExtraoralProfile, checklist.ExtraoralSmile,
                checklist.IntraoralFrontal, checklist.IntraoralRight, checklist.IntraoralLeft,
                checklist.UpperOcclusal, checklist.LowerOcclusal,
                checklist.Opg, checklist.LateralCeph, checklist.Cbct,
                checklist.StudyModels, checklist.Consent, checklist.Contract,
            }.Count(b => b);
        }
        else
        {
            // Auto-derive checklist items from existing data using same per-item logic as GET /checklist.
            // CLIN-15: fetch only the 4 columns actually used by OrthoPhotoRecords (Category, Subtype,
            // PhotoType, Caption) instead of pulling the full OrthoClinicalPhoto rows.
            var photos = await _db.OrthoClinicalPhotos
                .AsNoTracking()
                .Where(p => p.OrthoCaseId == id)
                .Select(p => new OrthoClinicalPhoto
                {
                    Category = p.Category,
                    Subtype = p.Subtype,
                    PhotoType = p.PhotoType,
                    Caption = p.Caption,
                })
                .ToListAsync();
            var hasCeph = overview.CephAnalysesCount > 0;
            var hasModel = overview.HasModelAnalysis;
            // Use precise per-item matching — same as GET /checklist derivation:
            // (Category, Subtype) tags first, legacy caption keywords as fallback
            checklistCompleted = new[]
            {
                OrthoPhotoRecords.DeriveExtraoralFrontal(photos),
                OrthoPhotoRecords.DeriveExtraoralProfile(photos),
                OrthoPhotoRecords.DeriveExtraoralSmile(photos),
                OrthoPhotoRecords.DeriveIntraoralFrontal(photos),
                OrthoPhotoRecords.DeriveIntraoralRight(photos),
                OrthoPhotoRecords.DeriveIntraoralLeft(photos),
                OrthoPhotoRecords.DeriveUpperOcclusal(photos),
                OrthoPhotoRecords.DeriveLowerOcclusal(photos),
                OrthoPhotoRecords.DeriveOpg(photos),
                hasCeph || OrthoPhotoRecords.DeriveLateralCeph(photos),
                OrthoPhotoRecords.DeriveCbct(photos),
                hasModel,
                false, // Consent — cannot auto-derive
                contract is not null,
            }.Count(b => b);
        }

        return new OrthoCaseOverviewDto
        {
            HasClinicalExam = overview.HasClinicalExam,
            ProblemsCount = overview.ProblemsCount,
            HasDiagnosis = hasDiagnosis,
            IsDiagnosisApproved = isDiagnosisApproved,
            HasTreatmentPlan = overview.TreatmentPlansCount > 0,
            IsTreatmentPlanApproved = overview.LatestPlanIsApproved,
            TreatmentPlansCount = overview.TreatmentPlansCount,
            CompletedStages = overview.CompletedStages,
            TotalStages = overview.TotalStages,
            VisitsCount = overview.VisitsCount,
            PhotosCount = overview.PhotosCount,
            CephAnalysesCount = overview.CephAnalysesCount,
            PhotoAnalysesCount = overview.PhotoAnalysesCount,
            LatestCephAnalysisId = overview.LatestCeph?.Id,
            LatestCephAnalysisDate = overview.LatestCeph?.AnalysisDate.ToString("yyyy-MM-dd"),
            CephDiagnosisSyncedAt = overview.Diagnosis?.CephSyncedAt,
            IsCephDiagnosisOutdated = overview.LatestCeph is not null &&
                overview.Diagnosis?.CephSourceAnalysisId != overview.LatestCeph.Id,
            LatestProfileAnalysisId = overview.LatestProfile?.Id,
            LatestProfileAnalysisDate = overview.LatestProfile?.CreatedAt.ToString("yyyy-MM-dd"),
            LatestFrontalAnalysisId = overview.LatestFrontal?.Id,
            LatestFrontalAnalysisDate = overview.LatestFrontal?.CreatedAt.ToString("yyyy-MM-dd"),
            PhotoAnalysisSyncedAt = overview.Diagnosis?.PhotoAnalysisSyncedAt,
            IsPhotoAnalysisSyncOutdated =
                (overview.LatestProfile is not null && overview.Diagnosis?.ProfileSourceAnalysisId != overview.LatestProfile.Id) ||
                (overview.LatestFrontal is not null && overview.Diagnosis?.FrontalSourceAnalysisId != overview.LatestFrontal.Id),
            HasRetention = overview.HasRetention,
            ChecklistCompleted = checklistCompleted,
            ChecklistTotal = checklistTotal,
            ContractId = contract?.Id,
            ContractTotal = contractTotal,
            ContractPaid = contractPaid,
            ContractRemaining = contractRemaining,
            LatestVisitDate = overview.LatestVisit?.VisitDate.ToString("yyyy-MM-dd"),
            NextAppointmentDate = overview.LatestVisit?.NextAppointmentDate?.ToString("yyyy-MM-dd"),
        };
    }

    // ─── GET /api/ortho-cases/{id}/clinical-exam ────────────────────────────

    /// <summary>
    /// Most recent clinical exam for the case. Returns <c>null</c> when no
    /// exam exists (controller maps to <c>Ok(null)</c> — preserving the
    /// original behavior).
    /// </summary>
    public async Task<OrthoClinicalExamDto?> GetClinicalExamAsync(Guid id)
    {
        var exam = await _db.OrthoClinicalExams
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
        if (exam is null) return null;
        return new OrthoClinicalExamDto
        {
            Id = exam.Id,
            ExamDate = exam.ExamDate.ToString("yyyy-MM-dd"),
            FacialSymmetry = exam.FacialSymmetry,
            Profile = exam.Profile,
            LipsCompetence = exam.LipsCompetence,
            SmileLine = exam.SmileLine,
            VerticalProportion = exam.VerticalProportion,
            MolarRelation = exam.MolarRelation,
            CanineRelation = exam.CanineRelation,
            Overjet = exam.Overjet,
            Overbite = exam.Overbite,
            Crossbite = exam.Crossbite,
            OpenBite = exam.OpenBite,
            UpperCrowding = exam.UpperCrowding,
            LowerCrowding = exam.LowerCrowding,
            UpperSpacing = exam.UpperSpacing,
            MidlineUpper = exam.MidlineUpper,
            MidlineLower = exam.MidlineLower,
            CoCrDiscrepancy = exam.CoCrDiscrepancy,
            TmjFindings = exam.TmjFindings,
            Habits = exam.Habits,
            Notes = exam.Notes,
            DoctorId = exam.DoctorId,
            // Phase 3 — structured clinical examination
            MolarRelationRight = exam.MolarRelationRight,
            MolarRelationLeft = exam.MolarRelationLeft,
            CanineRelationRight = exam.CanineRelationRight,
            CanineRelationLeft = exam.CanineRelationLeft,
            IncisorRelation = exam.IncisorRelation,
            OverbitePercent = exam.OverbitePercent,
            DeepBite = exam.DeepBite,
            CrossbiteType = exam.CrossbiteType,
            ScissorBite = exam.ScissorBite,
            MidlineUpperShiftMm = exam.MidlineUpperShiftMm,
            MidlineLowerShiftMm = exam.MidlineLowerShiftMm,
            UpperCrowdingMm = exam.UpperCrowdingMm,
            LowerCrowdingMm = exam.LowerCrowdingMm,
            LowerSpacingMm = exam.LowerSpacingMm,
            CurveOfSpee = exam.CurveOfSpee,
            ArchFormUpper = exam.ArchFormUpper,
            ArchFormLower = exam.ArchFormLower,
            BoltonDiscrepancyNote = exam.BoltonDiscrepancyNote,
            LipCompetenceGrade = exam.LipCompetenceGrade,
            NasolabialAngle = exam.NasolabialAngle,
            ChinPosition = exam.ChinPosition,
            FunctionalShift = exam.FunctionalShift,
            GummySmile = exam.GummySmile,
            ThumbSucking = exam.ThumbSucking,
            MouthBreathing = exam.MouthBreathing,
            TongueThrust = exam.TongueThrust,
            LipBiting = exam.LipBiting,
            NailBiting = exam.NailBiting,
            Bruxism = exam.Bruxism,
            OralHygiene = exam.OralHygiene,
            GingivalCondition = exam.GingivalCondition,
            PeriodontalConcerns = exam.PeriodontalConcerns,
            MissingTeethFdi = exam.MissingTeethFdi,
            RetainedDeciduousFdi = exam.RetainedDeciduousFdi,
            ImpactedTeethFdi = exam.ImpactedTeethFdi,
            SupernumeraryNote = exam.SupernumeraryNote,
            EctopicEruptionNote = exam.EctopicEruptionNote,
            FrenumNote = exam.FrenumNote,
            TongueNote = exam.TongueNote,
            CariesNote = exam.CariesNote,
        };
    }

    // ─── GET /api/ortho-cases/{id}/problem-list ─────────────────────────────

    public async Task<List<OrthoProblemListItemDto>> GetProblemListAsync(Guid id)
    {
        return await _db.ProblemListItems
            .Where(p => p.OrthoCaseId == id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt)
            .Select(p => new OrthoProblemListItemDto
            {
                Id = p.Id,
                Category = p.Category,
                Description = p.Description,
                Severity = p.Severity,
                SortOrder = p.SortOrder,
            })
            .ToListAsync();
    }

    // ─── GET /api/ortho-cases/{id}/treatment-plans ──────────────────────────

    public async Task<List<OrthoTreatmentPlanListItemDto>> GetTreatmentPlansAsync(Guid id)
    {
        return await _db.TreatmentPlans
            .Include(p => p.ApprovedByDoctor)
            .Where(p => p.OrthoCaseId == id)
            .OrderBy(p => p.PlanLabel)
            .Select(p => new OrthoTreatmentPlanListItemDto
            {
                Id = p.Id,
                PlanVersion = p.PlanVersion,
                PlanLabel = p.PlanLabel,
                IsApproved = p.IsApproved,
                ApplianceType = p.ApplianceType,
                BracketSystem = p.BracketSystem,
                InitialWire = p.InitialWire,
                ExtractionPlan = p.ExtractionPlan,
                AnchoragePlan = p.AnchoragePlan,
                UseTads = p.UseTads,
                UseElastics = p.UseElastics,
                ExpectedDurationMonths = p.ExpectedDurationMonths,
                RetentionPlan = p.RetentionPlan,
                TreatmentGoals = p.TreatmentGoals,
                RisksLimitations = p.RisksLimitations,
                ApprovedByName = p.ApprovedByDoctor != null ? p.ApprovedByDoctor.Name : null,
                ApprovedAt = p.ApprovedAt != null ? p.ApprovedAt.Value.ToString("yyyy-MM-dd") : null,
            })
            .ToListAsync();
    }

    // ─── GET /api/ortho-cases/{id}/treatment-plan ───────────────────────────

    /// <summary>
    /// Backward-compatible: returns the latest (or approved) single plan.
    /// Returns <c>null</c> when no plan exists (controller maps to
    /// <c>Ok(null)</c>).
    /// </summary>
    public async Task<OrthoTreatmentPlanDetailDto?> GetTreatmentPlanAsync(Guid id)
    {
        var plan = await _db.TreatmentPlans
            .Include(p => p.ApprovedByDoctor)
            .Where(p => p.OrthoCaseId == id)
            .OrderByDescending(p => p.IsApproved ? 1 : 0)
            .ThenByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
        if (plan is null) return null;
        return new OrthoTreatmentPlanDetailDto
        {
            Id = plan.Id,
            PlanVersion = plan.PlanVersion,
            PlanLabel = plan.PlanLabel,
            IsApproved = plan.IsApproved,
            ApplianceType = plan.ApplianceType,
            BracketSystem = plan.BracketSystem,
            InitialWire = plan.InitialWire,
            ExtractionPlan = plan.ExtractionPlan,
            AnchoragePlan = plan.AnchoragePlan,
            UseTads = plan.UseTads,
            UseElastics = plan.UseElastics,
            ExpectedDurationMonths = plan.ExpectedDurationMonths,
            RetentionPlan = plan.RetentionPlan,
            TreatmentGoals = plan.TreatmentGoals,
            RisksLimitations = plan.RisksLimitations,
            ApprovedByName = plan.ApprovedByDoctor?.Name,
            ApprovedAt = plan.ApprovedAt?.ToString("yyyy-MM-dd"),
        };
    }

    // ─── GET /api/ortho-cases/{id}/extraction-decision ──────────────────────

    /// <summary>
    /// Most recent extraction decision. Returns <c>null</c> when none exists
    /// (controller maps to <c>Ok(null)</c>).
    /// </summary>
    public async Task<OrthoExtractionDecisionDto?> GetExtractionDecisionAsync(Guid id)
    {
        var decision = await _db.ExtractionDecisions
            .Include(e => e.Doctor)
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
        if (decision is null) return null;
        return new OrthoExtractionDecisionDto
        {
            Id = decision.Id,
            Decision = decision.Decision,
            ProExtraction = decision.ProExtraction,
            ConExtraction = decision.ConExtraction,
            DoctorNotes = decision.DoctorNotes,
            AiRecommendation = decision.AiRecommendation,
            DecidedByName = decision.Doctor?.Name,
            DecidedAt = decision.DecidedAt?.ToString("yyyy-MM-dd"),
        };
    }

    // ─── GET /api/ortho-cases/{id}/checklist ────────────────────────────────

    /// <summary>
    /// Records checklist: returns the saved row if present, otherwise auto-
    /// derives each item from (Category, Subtype) photo tags with the legacy
    /// caption-keyword heuristic as an OR'd fallback (untagged photos keep
    /// working). Same per-item logic as <see cref="GetOverviewAsync"/>'s
    /// fallback path.
    /// </summary>
    public async Task<OrthoChecklistDto> GetChecklistAsync(Guid id)
    {
        var checklist = await _db.RecordsChecklists
            .Where(r => r.OrthoCaseId == id)
            .FirstOrDefaultAsync();

        if (checklist is not null)
        {
            return new OrthoChecklistDto
            {
                Id = checklist.Id,
                OrthoCaseId = checklist.OrthoCaseId,
                ExtraoralFrontal = checklist.ExtraoralFrontal,
                ExtraoralProfile = checklist.ExtraoralProfile,
                ExtraoralSmile = checklist.ExtraoralSmile,
                IntraoralFrontal = checklist.IntraoralFrontal,
                IntraoralRight = checklist.IntraoralRight,
                IntraoralLeft = checklist.IntraoralLeft,
                UpperOcclusal = checklist.UpperOcclusal,
                LowerOcclusal = checklist.LowerOcclusal,
                Opg = checklist.Opg,
                LateralCeph = checklist.LateralCeph,
                Cbct = checklist.Cbct,
                StudyModels = checklist.StudyModels,
                Consent = checklist.Consent,
                Contract = checklist.Contract,
            };
        }

        // Auto-derive from existing data if no checklist exists yet
        var photos = await _db.OrthoClinicalPhotos.Where(p => p.OrthoCaseId == id).ToListAsync();
        var hasCeph = await _db.CephAnalyses.AnyAsync(c => c.OrthoCaseId == id);
        var hasModel = await _db.ModelAnalyses.AnyAsync(m => m.OrthoCaseId == id);
        var patientId = await _db.OrthoCases.Where(oc => oc.Id == id).Select(oc => oc.PatientId).FirstOrDefaultAsync();
        var contract = await _db.Contracts
            .Where(c => c.PatientId == patientId && c.RelatedCaseId == id)
            .AnyAsync();
        if (!contract && patientId != Guid.Empty)
        {
            var unlinkedCount = await _db.Contracts
                .Where(c => c.PatientId == patientId && c.RelatedCaseId == null &&
                    (c.Specialty == "orthodontics" || c.Specialty == "ortho"))
                .CountAsync();
            contract = unlinkedCount == 1;
        }

        // Auto-derive each item from (Category, Subtype) tags with the legacy
        // caption-keyword heuristic as an OR'd fallback (untagged photos keep working)
        return new OrthoChecklistDto
        {
            Id = null,
            OrthoCaseId = id,
            ExtraoralFrontal = OrthoPhotoRecords.DeriveExtraoralFrontal(photos),
            ExtraoralProfile = OrthoPhotoRecords.DeriveExtraoralProfile(photos),
            ExtraoralSmile = OrthoPhotoRecords.DeriveExtraoralSmile(photos),
            IntraoralFrontal = OrthoPhotoRecords.DeriveIntraoralFrontal(photos),
            IntraoralRight = OrthoPhotoRecords.DeriveIntraoralRight(photos),
            IntraoralLeft = OrthoPhotoRecords.DeriveIntraoralLeft(photos),
            UpperOcclusal = OrthoPhotoRecords.DeriveUpperOcclusal(photos),
            LowerOcclusal = OrthoPhotoRecords.DeriveLowerOcclusal(photos),
            Opg = OrthoPhotoRecords.DeriveOpg(photos),
            LateralCeph = hasCeph || OrthoPhotoRecords.DeriveLateralCeph(photos),
            Cbct = OrthoPhotoRecords.DeriveCbct(photos),
            StudyModels = hasModel,
            Consent = false,
            Contract = contract,
        };
    }

    // ─── GET /api/ortho-cases/{id}/diagnosis ────────────────────────────────

    /// <summary>
    /// Diagnosis: returns the saved diagnosis (with sync-status flags) if it
    /// exists; otherwise computes a summary from ClinicalExam, ProblemList,
    /// and CephAnalysis measurements. Returns <c>null</c> if the case itself
    /// does not exist (controller maps to 404 with the Arabic message).
    /// </summary>
    public async Task<OrthoDiagnosisDto?> GetDiagnosisAsync(Guid id)
    {
        var latestCephInfo = await _db.CephAnalyses
            .AsNoTracking()
            .Where(a => a.OrthoCaseId == id)
            .OrderByDescending(a => a.AnalysisDate)
            .ThenByDescending(a => a.UpdatedAt)
            .Select(a => new
            {
                a.Id,
                AnalysisDate = a.AnalysisDate.ToString("yyyy-MM-dd"),
            })
            .FirstOrDefaultAsync();
        var latestProfile = await _db.PhotoAnalyses
            .AsNoTracking()
            .Where(a => a.OrthoCaseId == id && a.ViewType == "profile")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                AnalysisDate = a.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .FirstOrDefaultAsync();
        var latestFrontal = await _db.PhotoAnalyses
            .AsNoTracking()
            .Where(a => a.OrthoCaseId == id && a.ViewType == "frontal")
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                AnalysisDate = a.CreatedAt.ToString("yyyy-MM-dd"),
            })
            .FirstOrDefaultAsync();

        var diagnosis = await _db.OrthoDiagnoses
            .Include(d => d.ApprovedByDoctor)
            .Where(d => d.OrthoCaseId == id)
            .FirstOrDefaultAsync();

        if (diagnosis is not null)
        {
            return new OrthoDiagnosisDto
            {
                Id = diagnosis.Id,
                SkeletalClassification = diagnosis.SkeletalClassification,
                DentalClassification = diagnosis.DentalClassification,
                FacialPattern = diagnosis.FacialPattern,
                SoftTissueDiagnosis = diagnosis.SoftTissueDiagnosis,
                FunctionalDiagnosis = diagnosis.FunctionalDiagnosis,
                Etiology = diagnosis.Etiology,
                ANB = diagnosis.ANB,
                Wits = diagnosis.Wits,
                FMA = diagnosis.FMA,
                SNA = diagnosis.SNA,
                SNB = diagnosis.SNB,
                IMPA = diagnosis.IMPA,
                Summary = diagnosis.Summary,
                CephSourceAnalysisId = diagnosis.CephSourceAnalysisId,
                CephSyncedAt = diagnosis.CephSyncedAt,
                PhotoAnalysisSummary = diagnosis.PhotoAnalysisSummary,
                ProfileSourceAnalysisId = diagnosis.ProfileSourceAnalysisId,
                FrontalSourceAnalysisId = diagnosis.FrontalSourceAnalysisId,
                PhotoAnalysisSyncedAt = diagnosis.PhotoAnalysisSyncedAt,
                LatestCephAnalysisId = latestCephInfo?.Id,
                LatestCephAnalysisDate = latestCephInfo?.AnalysisDate,
                IsCephSyncOutdated = latestCephInfo is not null &&
                    diagnosis.CephSourceAnalysisId != latestCephInfo.Id,
                LatestProfileAnalysisId = latestProfile?.Id,
                LatestProfileAnalysisDate = latestProfile?.AnalysisDate,
                LatestFrontalAnalysisId = latestFrontal?.Id,
                LatestFrontalAnalysisDate = latestFrontal?.AnalysisDate,
                IsPhotoAnalysisSyncOutdated =
                    (latestProfile is not null && diagnosis.ProfileSourceAnalysisId != latestProfile.Id) ||
                    (latestFrontal is not null && diagnosis.FrontalSourceAnalysisId != latestFrontal.Id),
                IsApproved = diagnosis.ApprovedAt != null,
                ApprovedByName = diagnosis.ApprovedByDoctor?.Name,
                ApprovedAt = diagnosis.ApprovedAt?.ToString("yyyy-MM-dd"),
            };
        }

        // Compute a summary from ClinicalExam, ProblemList, and CephAnalysis measurements
        var orthoCase = await _db.OrthoCases
            .Include(c => c.ClinicalExam)
            .Include(c => c.ProblemList)
            .Include(c => c.CephAnalyses)
                .ThenInclude(a => a.Measurements)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (orthoCase is null) return null;

        var latestCeph = orthoCase.CephAnalyses
            .OrderByDescending(a => a.AnalysisDate)
            .FirstOrDefault();
        var measurements = latestCeph?.Measurements ?? [];

        var anbValue = measurements.FirstOrDefault(m => m.MeasurementName == "ANB")?.MeasurementValue;
        var witsValue = measurements.FirstOrDefault(m => m.MeasurementName == "Wits")?.MeasurementValue;
        var fmaValue = measurements.FirstOrDefault(m => m.MeasurementName == "FMA")?.MeasurementValue;
        var snaValue = measurements.FirstOrDefault(m => m.MeasurementName == "SNA")?.MeasurementValue;
        var snbValue = measurements.FirstOrDefault(m => m.MeasurementName == "SNB")?.MeasurementValue;
        var impaValue = measurements.FirstOrDefault(m => m.MeasurementName == "IMPA")?.MeasurementValue;

        string? skeletalClass = anbValue switch
        {
            >= 0 and <= 4 => "Class I",
            > 4 => "Class II",
            < 0 => "Class III",
            null => null
        };

        string? facialPattern = fmaValue switch
        {
            < 22 => "Hypodivergent",
            >= 22 and <= 28 => "Normodivergent",
            > 28 => "Hyperdivergent",
            null => null
        };

        string? dentalClass = orthoCase.ClinicalExam?.MolarRelation;

        var problemSummary = orthoCase.ProblemList.Count > 0
            ? string.Join("، ", orthoCase.ProblemList.OrderBy(p => p.SortOrder).Select(p => p.Description))
            : null;

        return new OrthoDiagnosisDto
        {
            Id = null,
            SkeletalClassification = skeletalClass,
            DentalClassification = dentalClass,
            FacialPattern = facialPattern,
            SoftTissueDiagnosis = null,
            FunctionalDiagnosis = null,
            Etiology = null,
            ANB = anbValue,
            Wits = witsValue,
            FMA = fmaValue,
            SNA = snaValue,
            SNB = snbValue,
            IMPA = impaValue,
            Summary = problemSummary,
            CephSourceAnalysisId = null,
            CephSyncedAt = null,
            PhotoAnalysisSummary = null,
            ProfileSourceAnalysisId = null,
            FrontalSourceAnalysisId = null,
            PhotoAnalysisSyncedAt = null,
            LatestCephAnalysisId = latestCephInfo?.Id,
            LatestCephAnalysisDate = latestCephInfo?.AnalysisDate,
            IsCephSyncOutdated = latestCephInfo is not null,
            LatestProfileAnalysisId = latestProfile?.Id,
            LatestProfileAnalysisDate = latestProfile?.AnalysisDate,
            LatestFrontalAnalysisId = latestFrontal?.Id,
            LatestFrontalAnalysisDate = latestFrontal?.AnalysisDate,
            IsPhotoAnalysisSyncOutdated = latestProfile is not null || latestFrontal is not null,
            IsApproved = false,
            ApprovedByName = null,
            ApprovedAt = null,
        };
    }

    // ─── GET /api/ortho-cases/{id}/retention ────────────────────────────────

    /// <summary>
    /// Retention record with its visits. Returns <c>null</c> when no record
    /// exists (controller maps to <c>Ok(null)</c>).
    /// </summary>
    public async Task<OrthoRetentionDto?> GetRetentionAsync(Guid id)
    {
        var record = await _db.RetentionRecords
            .Include(r => r.Visits)
            .Where(r => r.OrthoCaseId == id)
            .FirstOrDefaultAsync();
        if (record is null) return null;
        return new OrthoRetentionDto
        {
            Id = record.Id,
            DebondDate = record.DebondDate?.ToString("yyyy-MM-dd"),
            UpperRetainer = record.UpperRetainer,
            LowerRetainer = record.LowerRetainer,
            Instructions = record.Instructions,
            Status = record.Status,
            Visits = record.Visits
                .OrderBy(v => v.VisitDate)
                .Select(v => new OrthoRetentionVisitDto
                {
                    Id = v.Id,
                    VisitDate = v.VisitDate?.ToString("yyyy-MM-dd"),
                    Period = v.Period,
                    ToothStability = v.ToothStability,
                    RetainerStatus = v.RetainerStatus,
                    Notes = v.Notes,
                })
                .ToList(),
        };
    }

    // ─── GET /api/ortho-cases/{id}/retention/visits ─────────────────────────

    public async Task<List<OrthoRetentionVisitDto>> GetRetentionVisitsAsync(Guid id)
    {
        return await _db.RetentionVisits
            .Where(v => v.RetentionRecord.OrthoCaseId == id)
            .OrderByDescending(v => v.VisitDate)
            .Select(v => new OrthoRetentionVisitDto
            {
                Id = v.Id,
                VisitDate = v.VisitDate != null ? v.VisitDate.Value.ToString("yyyy-MM-dd") : null,
                Period = v.Period,
                ToothStability = v.ToothStability,
                RetainerStatus = v.RetainerStatus,
                Notes = v.Notes,
            })
            .ToListAsync();
    }

    // ─── GET /api/ortho-cases/{id}/photos ───────────────────────────────────

    /// <summary>
    /// Clinical photos for the case with optional category/phase/selected filters.
    /// <para>
    /// <b>Inputs are already-normalized values</b> — the controller runs
    /// <c>OrthoPhotoRecords.TryNormalizeCategory</c> /
    /// <c>TryNormalizePhase</c> (which return Arabic BadRequest on invalid
    /// input) BEFORE delegating here. The service never sees invalid enum
    /// names. Pass <c>null</c> to skip the corresponding filter.
    /// </para>
    /// </summary>
    public async Task<List<OrthoClinicalPhotoDto>> GetPhotosAsync(
        Guid id,
        string? normalizedCategory,
        string? normalizedPhase,
        bool? selectedOnly)
    {
        var query = _db.OrthoClinicalPhotos.Where(p => p.OrthoCaseId == id);
        if (normalizedCategory is not null)
            query = query.Where(p => p.Category == normalizedCategory);
        if (normalizedPhase is not null)
            query = query.Where(p => p.TreatmentPhase == normalizedPhase);
        if (selectedOnly == true)
            query = query.Where(p => p.IsSelectedForReport);

        return await query
            .OrderBy(p => p.SortOrder).ThenBy(p => p.TakenAt)
            .Select(p => new OrthoClinicalPhotoDto
            {
                Id = p.Id,
                PhotoUrl = p.PhotoUrl,
                PhotoType = p.PhotoType,
                Caption = p.Caption,
                TakenAt = p.TakenAt.ToString("yyyy-MM-dd"),
                SortOrder = p.SortOrder,
                Category = p.Category,
                Subtype = p.Subtype,
                TreatmentPhase = p.TreatmentPhase,
                IsSelectedForReport = p.IsSelectedForReport,
                PreparationStatus = p.ImagePreparation != null
                    ? p.ImagePreparation.Status
                    : "OriginalUploaded",
                IsPreparedForReport = p.ImagePreparation != null,
                PreparedImageUrl = p.ImagePreparation != null
                    ? p.ImagePreparation.PreparedImageUrl
                    : null,
            })
            .ToListAsync();
    }

    // ─── GET /api/ortho-cases/{id}/photos/{photoId}/preparation ────────────

    /// <summary>
    /// Image-preparation metadata for a single photo. Returns <c>null</c> if
    /// the photo doesn't exist or doesn't belong to this case (controller
    /// maps to 404 with the Arabic message).
    /// </summary>
    public async Task<OrthoImagePreparationDto?> GetImagePreparationAsync(Guid id, Guid photoId)
    {
        var photo = await _db.OrthoClinicalPhotos
            .AsNoTracking()
            .Include(p => p.ImagePreparation)
            .FirstOrDefaultAsync(p => p.Id == photoId && p.OrthoCaseId == id);
        if (photo is null) return null;
        return MapImagePreparation(photo, photo.ImagePreparation);
    }

    // ─── Shared projection helper ───────────────────────────────────────────
    // Extracted verbatim from OrthoCasesController.MapImagePreparation.
    // Used by GetImagePreparationAsync (read path) — also still used directly by the
    // controller for SaveImagePreparation / ResetImagePreparation (which are NOT
    // extracted: they are write/mutation endpoints).

    /// <summary>
    /// Projects an <see cref="OrthoClinicalPhoto"/> + (optional)
    /// <see cref="OrthoImagePreparation"/> into the response DTO shape used by
    /// the photo preparation endpoints. Defaults mirror the original
    /// controller's anonymous-type projection exactly (crop 0/0/1/1, zoom 1,
    /// rotation 0, brightness/contrast 0, no flips, "Original" aspect ratio,
    /// "OriginalUploaded" status).
    /// </summary>
    public static OrthoImagePreparationDto MapImagePreparation(
        OrthoClinicalPhoto photo,
        OrthoImagePreparation? preparation) => new()
    {
        PhotoId = photo.Id,
        OriginalPhotoUrl = photo.PhotoUrl,
        PreparedImageUrl = preparation?.PreparedImageUrl,
        CropX = preparation?.CropX ?? 0m,
        CropY = preparation?.CropY ?? 0m,
        CropWidth = preparation?.CropWidth ?? 1m,
        CropHeight = preparation?.CropHeight ?? 1m,
        Zoom = preparation?.Zoom ?? 1m,
        RotationDegrees = preparation?.RotationDegrees ?? 0,
        Brightness = preparation?.Brightness ?? 0,
        Contrast = preparation?.Contrast ?? 0,
        FlipHorizontal = preparation?.FlipHorizontal ?? false,
        FlipVertical = preparation?.FlipVertical ?? false,
        AspectRatio = preparation?.AspectRatio ?? "Original",
        Preset = preparation?.Preset,
        Status = preparation?.Status ?? "OriginalUploaded",
        PreparedAt = preparation?.PreparedAt,
        ApprovedAt = preparation?.ApprovedAt,
    };
}

// ─── DTOs ────────────────────────────────────────────────────────────────────
// Property names match the original controller's anonymous-type projections
// exactly, so the JSON response shape (camelCase via System.Text.Json default)
// is unchanged. Each DTO corresponds to one endpoint's response shape.

/// <summary>GET /api/ortho-cases/{id}/lab-orders — read-only lab orders linked to this case.</summary>
public sealed record OrthoCaseLabOrderDto
{
    public Guid Id { get; init; }
    public string? OrderNumber { get; init; }
    public string? ApplianceType { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string? LabName { get; init; }
    public decimal? TotalCost { get; init; }
    // DateOnly? preserved as-is (System.Text.Json serializes as ISO date "yyyy-MM-dd").
    public DateOnly? SentDate { get; init; }
    public DateOnly? ExpectedDate { get; init; }
    public DateOnly? ReceivedDate { get; init; }
    public DateOnly? DeliveredDate { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/overview — single-projection case overview.</summary>
public sealed record OrthoCaseOverviewDto
{
    public bool HasClinicalExam { get; init; }
    public int ProblemsCount { get; init; }
    public bool HasDiagnosis { get; init; }
    public bool IsDiagnosisApproved { get; init; }
    public bool HasTreatmentPlan { get; init; }
    public bool IsTreatmentPlanApproved { get; init; }
    public int TreatmentPlansCount { get; init; }
    public int CompletedStages { get; init; }
    public int TotalStages { get; init; }
    public int VisitsCount { get; init; }
    public int PhotosCount { get; init; }
    public int CephAnalysesCount { get; init; }
    public int PhotoAnalysesCount { get; init; }
    public Guid? LatestCephAnalysisId { get; init; }
    public string? LatestCephAnalysisDate { get; init; }
    public DateTime? CephDiagnosisSyncedAt { get; init; }
    public bool IsCephDiagnosisOutdated { get; init; }
    public Guid? LatestProfileAnalysisId { get; init; }
    public string? LatestProfileAnalysisDate { get; init; }
    public Guid? LatestFrontalAnalysisId { get; init; }
    public string? LatestFrontalAnalysisDate { get; init; }
    public DateTime? PhotoAnalysisSyncedAt { get; init; }
    public bool IsPhotoAnalysisSyncOutdated { get; init; }
    public bool HasRetention { get; init; }
    public int ChecklistCompleted { get; init; }
    public int ChecklistTotal { get; init; }
    public Guid? ContractId { get; init; }
    public decimal? ContractTotal { get; init; }
    public decimal? ContractPaid { get; init; }
    public decimal? ContractRemaining { get; init; }
    public string? LatestVisitDate { get; init; }
    public string? NextAppointmentDate { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/clinical-exam — latest clinical exam.</summary>
public sealed record OrthoClinicalExamDto
{
    public Guid Id { get; init; }
    public string ExamDate { get; init; } = string.Empty;
    public string? FacialSymmetry { get; init; }
    public string? Profile { get; init; }
    public bool? LipsCompetence { get; init; }
    public string? SmileLine { get; init; }
    public string? VerticalProportion { get; init; }
    public string? MolarRelation { get; init; }
    public string? CanineRelation { get; init; }
    public decimal? Overjet { get; init; }
    public decimal? Overbite { get; init; }
    public bool Crossbite { get; init; }
    public bool OpenBite { get; init; }
    public string? UpperCrowding { get; init; }
    public string? LowerCrowding { get; init; }
    public decimal? UpperSpacing { get; init; }
    public string? MidlineUpper { get; init; }
    public string? MidlineLower { get; init; }
    public bool? CoCrDiscrepancy { get; init; }
    public string? TmjFindings { get; init; }
    public string? Habits { get; init; }
    public string? Notes { get; init; }
    public Guid? DoctorId { get; init; }
    // Phase 3 — structured clinical examination
    public string? MolarRelationRight { get; init; }
    public string? MolarRelationLeft { get; init; }
    public string? CanineRelationRight { get; init; }
    public string? CanineRelationLeft { get; init; }
    public string? IncisorRelation { get; init; }
    public decimal? OverbitePercent { get; init; }
    public bool? DeepBite { get; init; }
    public string? CrossbiteType { get; init; }
    public bool? ScissorBite { get; init; }
    public decimal? MidlineUpperShiftMm { get; init; }
    public decimal? MidlineLowerShiftMm { get; init; }
    public decimal? UpperCrowdingMm { get; init; }
    public decimal? LowerCrowdingMm { get; init; }
    public decimal? LowerSpacingMm { get; init; }
    public string? CurveOfSpee { get; init; }
    public string? ArchFormUpper { get; init; }
    public string? ArchFormLower { get; init; }
    public string? BoltonDiscrepancyNote { get; init; }
    public string? LipCompetenceGrade { get; init; }
    public string? NasolabialAngle { get; init; }
    public string? ChinPosition { get; init; }
    public string? FunctionalShift { get; init; }
    public bool? GummySmile { get; init; }
    public bool? ThumbSucking { get; init; }
    public bool? MouthBreathing { get; init; }
    public bool? TongueThrust { get; init; }
    public bool? LipBiting { get; init; }
    public bool? NailBiting { get; init; }
    public bool? Bruxism { get; init; }
    public string? OralHygiene { get; init; }
    public string? GingivalCondition { get; init; }
    public string? PeriodontalConcerns { get; init; }
    public string? MissingTeethFdi { get; init; }
    public string? RetainedDeciduousFdi { get; init; }
    public string? ImpactedTeethFdi { get; init; }
    public string? SupernumeraryNote { get; init; }
    public string? EctopicEruptionNote { get; init; }
    public string? FrenumNote { get; init; }
    public string? TongueNote { get; init; }
    public string? CariesNote { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/problem-list — problem list item.</summary>
public sealed record OrthoProblemListItemDto
{
    public Guid Id { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Severity { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/treatment-plans — list item (with approval metadata).</summary>
public sealed record OrthoTreatmentPlanListItemDto
{
    public Guid Id { get; init; }
    public int PlanVersion { get; init; }
    public string PlanLabel { get; init; } = string.Empty;
    public bool IsApproved { get; init; }
    public string? ApplianceType { get; init; }
    public string? BracketSystem { get; init; }
    public string? InitialWire { get; init; }
    public string? ExtractionPlan { get; init; }
    public string? AnchoragePlan { get; init; }
    public bool UseTads { get; init; }
    public bool UseElastics { get; init; }
    public int? ExpectedDurationMonths { get; init; }
    public string? RetentionPlan { get; init; }
    public string? TreatmentGoals { get; init; }
    public string? RisksLimitations { get; init; }
    public string? ApprovedByName { get; init; }
    public string? ApprovedAt { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/treatment-plan — latest/approved single plan.</summary>
public sealed record OrthoTreatmentPlanDetailDto
{
    public Guid Id { get; init; }
    public int PlanVersion { get; init; }
    public string PlanLabel { get; init; } = string.Empty;
    public bool IsApproved { get; init; }
    public string? ApplianceType { get; init; }
    public string? BracketSystem { get; init; }
    public string? InitialWire { get; init; }
    public string? ExtractionPlan { get; init; }
    public string? AnchoragePlan { get; init; }
    public bool UseTads { get; init; }
    public bool UseElastics { get; init; }
    public int? ExpectedDurationMonths { get; init; }
    public string? RetentionPlan { get; init; }
    public string? TreatmentGoals { get; init; }
    public string? RisksLimitations { get; init; }
    public string? ApprovedByName { get; init; }
    public string? ApprovedAt { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/extraction-decision — latest extraction decision.</summary>
public sealed record OrthoExtractionDecisionDto
{
    public Guid Id { get; init; }
    public string? Decision { get; init; }
    public System.Text.Json.JsonDocument? ProExtraction { get; init; }
    public System.Text.Json.JsonDocument? ConExtraction { get; init; }
    public string? DoctorNotes { get; init; }
    public string? AiRecommendation { get; init; }
    public string? DecidedByName { get; init; }
    public string? DecidedAt { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/checklist — saved checklist or auto-derived.</summary>
public sealed record OrthoChecklistDto
{
    public Guid? Id { get; init; }
    public Guid OrthoCaseId { get; init; }
    public bool ExtraoralFrontal { get; init; }
    public bool ExtraoralProfile { get; init; }
    public bool ExtraoralSmile { get; init; }
    public bool IntraoralFrontal { get; init; }
    public bool IntraoralRight { get; init; }
    public bool IntraoralLeft { get; init; }
    public bool UpperOcclusal { get; init; }
    public bool LowerOcclusal { get; init; }
    public bool Opg { get; init; }
    public bool LateralCeph { get; init; }
    public bool Cbct { get; init; }
    public bool StudyModels { get; init; }
    public bool Consent { get; init; }
    public bool Contract { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/diagnosis — saved diagnosis or computed summary.</summary>
public sealed record OrthoDiagnosisDto
{
    public Guid? Id { get; init; }
    public string? SkeletalClassification { get; init; }
    public string? DentalClassification { get; init; }
    public string? FacialPattern { get; init; }
    public string? SoftTissueDiagnosis { get; init; }
    public string? FunctionalDiagnosis { get; init; }
    public string? Etiology { get; init; }
    public decimal? ANB { get; init; }
    public decimal? Wits { get; init; }
    public decimal? FMA { get; init; }
    public decimal? SNA { get; init; }
    public decimal? SNB { get; init; }
    public decimal? IMPA { get; init; }
    public string? Summary { get; init; }
    public Guid? CephSourceAnalysisId { get; init; }
    public DateTime? CephSyncedAt { get; init; }
    public string? PhotoAnalysisSummary { get; init; }
    public Guid? ProfileSourceAnalysisId { get; init; }
    public Guid? FrontalSourceAnalysisId { get; init; }
    public DateTime? PhotoAnalysisSyncedAt { get; init; }
    public Guid? LatestCephAnalysisId { get; init; }
    public string? LatestCephAnalysisDate { get; init; }
    public bool IsCephSyncOutdated { get; init; }
    public Guid? LatestProfileAnalysisId { get; init; }
    public string? LatestProfileAnalysisDate { get; init; }
    public Guid? LatestFrontalAnalysisId { get; init; }
    public string? LatestFrontalAnalysisDate { get; init; }
    public bool IsPhotoAnalysisSyncOutdated { get; init; }
    public bool IsApproved { get; init; }
    public string? ApprovedByName { get; init; }
    public string? ApprovedAt { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/retention — retention record with visits.</summary>
public sealed record OrthoRetentionDto
{
    public Guid Id { get; init; }
    public string? DebondDate { get; init; }
    public string? UpperRetainer { get; init; }
    public string? LowerRetainer { get; init; }
    public string? Instructions { get; init; }
    public string Status { get; init; } = string.Empty;
    public List<OrthoRetentionVisitDto> Visits { get; init; } = new();
}

/// <summary>GET /api/ortho-cases/{id}/retention/visits — list item; also nested in OrthoRetentionDto.</summary>
public sealed record OrthoRetentionVisitDto
{
    public Guid Id { get; init; }
    public string? VisitDate { get; init; }
    public string? Period { get; init; }
    public string? ToothStability { get; init; }
    public string? RetainerStatus { get; init; }
    public string? Notes { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/photos — clinical photo list item.</summary>
public sealed record OrthoClinicalPhotoDto
{
    public Guid Id { get; init; }
    public string PhotoUrl { get; init; } = string.Empty;
    public string PhotoType { get; init; } = string.Empty;
    public string? Caption { get; init; }
    public string TakenAt { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public string? Category { get; init; }
    public string? Subtype { get; init; }
    public string? TreatmentPhase { get; init; }
    public bool IsSelectedForReport { get; init; }
    public string PreparationStatus { get; init; } = string.Empty;
    public bool IsPreparedForReport { get; init; }
    public string? PreparedImageUrl { get; init; }
}

/// <summary>GET /api/ortho-cases/{id}/photos/{photoId}/preparation — image preparation metadata.</summary>
public sealed record OrthoImagePreparationDto
{
    public Guid PhotoId { get; init; }
    public string OriginalPhotoUrl { get; init; } = string.Empty;
    public string? PreparedImageUrl { get; init; }
    public decimal CropX { get; init; }
    public decimal CropY { get; init; }
    public decimal CropWidth { get; init; } = 1m;
    public decimal CropHeight { get; init; } = 1m;
    public decimal Zoom { get; init; } = 1m;
    public int RotationDegrees { get; init; }
    public int Brightness { get; init; }
    public int Contrast { get; init; }
    public bool FlipHorizontal { get; init; }
    public bool FlipVertical { get; init; }
    public string AspectRatio { get; init; } = "Original";
    public string? Preset { get; init; }
    public string Status { get; init; } = "OriginalUploaded";
    public DateTime? PreparedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
}
