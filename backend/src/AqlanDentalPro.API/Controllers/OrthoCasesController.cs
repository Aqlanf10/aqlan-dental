using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public sealed class UpsertClinicalExamRequest
{
    public string? ExamDate { get; init; }
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
}

[ApiController]
[Route("api/ortho-cases")]
[Authorize(Policy = "OrthoAccess")]
public class OrthoCasesController(OrthoService service, AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? doctorId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        [FromQuery] Guid? patientId = null)
    {
        var result = await service.GetListAsync(page, pageSize, doctorId, status, search, patientId);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await service.GetByIdAsync(id);
        return result == null ? NotFound(new { message = "الحالة التقويمية غير موجودة" }) : Ok(result);
    }

    [HttpGet("{id:guid}/overview")]
    public async Task<IActionResult> GetOverview(Guid id)
    {
        var orthoCase = await db.OrthoCases
            .AsNoTracking()
            .Include(c => c.TreatmentPlans)
            .Include(c => c.Stages)
            .Include(c => c.Visits)
            .Include(c => c.OrthoClinicalPhotos)
            .Include(c => c.CephAnalyses)
            .Include(c => c.RetentionRecord)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var hasClinicalExam = await db.OrthoClinicalExams.AnyAsync(e => e.OrthoCaseId == id);
        var problemsCount = await db.ProblemListItems.CountAsync(p => p.OrthoCaseId == id);
        var hasDiagnosis = await db.OrthoDiagnoses.AnyAsync(d => d.OrthoCaseId == id);
        var latestPlan = orthoCase.TreatmentPlans.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
        var latestVisit = orthoCase.Visits.OrderByDescending(v => v.VisitDate).FirstOrDefault();

        var contract = await db.Contracts
            .Include(c => c.Payments)
            .Where(c => c.PatientId == orthoCase.PatientId &&
                (c.RelatedCaseId == id || c.Specialty == "orthodontics" || c.Specialty == "ortho"))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        decimal? contractTotal = null;
        decimal? contractPaid = null;
        decimal? contractRemaining = null;
        if (contract is not null)
        {
            contractTotal = contract.TotalAmount - contract.DiscountAmount;
            contractPaid = contract.Payments.Sum(p => p.Amount);
            contractRemaining = Math.Max(0, contractTotal.Value - contractPaid.Value);
        }

        return Ok(new
        {
            HasClinicalExam = hasClinicalExam,
            ProblemsCount = problemsCount,
            HasDiagnosis = hasDiagnosis,
            HasTreatmentPlan = latestPlan is not null,
            IsTreatmentPlanApproved = latestPlan?.IsApproved ?? false,
            CompletedStages = orthoCase.Stages.Count(s => s.Status == "completed"),
            TotalStages = orthoCase.Stages.Count,
            VisitsCount = orthoCase.Visits.Count,
            PhotosCount = orthoCase.OrthoClinicalPhotos.Count,
            CephAnalysesCount = orthoCase.CephAnalyses.Count,
            HasRetention = orthoCase.RetentionRecord is not null,
            ContractId = contract?.Id,
            ContractTotal = contractTotal,
            ContractPaid = contractPaid,
            ContractRemaining = contractRemaining,
            LatestVisitDate = latestVisit?.VisitDate.ToString("yyyy-MM-dd"),
            NextAppointmentDate = latestVisit?.NextAppointmentDate?.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrthoCaseRequest req)
    {
        var result = await service.CreateAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet("{id:guid}/visits")]
    public async Task<IActionResult> GetVisits(Guid id)
    {
        var result = await service.GetVisitsAsync(id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/visits")]
    public async Task<IActionResult> AddVisit(Guid id, [FromBody] CreateOrthoVisitRequest req)
    {
        var result = await service.AddVisitAsync(id, req);
        return Ok(result);
    }

    [HttpGet("{id:guid}/stages")]
    public async Task<IActionResult> GetStages(Guid id)
    {
        var result = await service.GetStagesAsync(id);
        return Ok(result);
    }

    [HttpPut("{id:guid}/stages/{stageId:guid}")]
    public async Task<IActionResult> UpdateStage(Guid id, Guid stageId, [FromBody] UpdateStageRequest req)
    {
        var result = await service.UpdateStageAsync(stageId, req.Status);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/clinical-exam")]
    public async Task<IActionResult> GetClinicalExam(Guid id)
    {
        var exam = await db.OrthoClinicalExams
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
        if (exam is null) return Ok(null);
        return Ok(new {
            exam.Id,
            ExamDate            = exam.ExamDate.ToString("yyyy-MM-dd"),
            exam.FacialSymmetry,
            exam.Profile,
            exam.LipsCompetence,
            exam.SmileLine,
            exam.VerticalProportion,
            exam.MolarRelation,
            exam.CanineRelation,
            exam.Overjet,
            exam.Overbite,
            exam.Crossbite,
            exam.OpenBite,
            exam.UpperCrowding,
            exam.LowerCrowding,
            exam.UpperSpacing,
            exam.MidlineUpper,
            exam.MidlineLower,
            exam.CoCrDiscrepancy,
            exam.TmjFindings,
            exam.Habits,
            exam.Notes,
            exam.DoctorId,
        });
    }

    [HttpPut("{id:guid}/clinical-exam")]
    public async Task<IActionResult> UpsertClinicalExam(Guid id, [FromBody] UpsertClinicalExamRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.OrthoClinicalExams
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            existing = new OrthoClinicalExam { OrthoCaseId = id };
            db.OrthoClinicalExams.Add(existing);
        }

        existing.ExamDate           = req.ExamDate != null ? DateOnly.Parse(req.ExamDate) : DateOnly.FromDateTime(DateTime.Today);
        existing.FacialSymmetry     = req.FacialSymmetry;
        existing.Profile            = req.Profile;
        existing.LipsCompetence     = req.LipsCompetence;
        existing.SmileLine          = req.SmileLine;
        existing.VerticalProportion = req.VerticalProportion;
        existing.MolarRelation      = req.MolarRelation;
        existing.CanineRelation     = req.CanineRelation;
        existing.Overjet            = req.Overjet;
        existing.Overbite           = req.Overbite;
        existing.Crossbite          = req.Crossbite;
        existing.OpenBite           = req.OpenBite;
        existing.UpperCrowding      = req.UpperCrowding;
        existing.LowerCrowding      = req.LowerCrowding;
        existing.UpperSpacing       = req.UpperSpacing;
        existing.MidlineUpper       = req.MidlineUpper;
        existing.MidlineLower       = req.MidlineLower;
        existing.CoCrDiscrepancy    = req.CoCrDiscrepancy;
        existing.TmjFindings        = req.TmjFindings;
        existing.Habits             = req.Habits;
        existing.Notes              = req.Notes;
        existing.DoctorId           = req.DoctorId;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ الفحص السريري" });
    }

    // ─── Problem List ────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/problem-list")]
    public async Task<IActionResult> GetProblemList(Guid id)
    {
        var items = await db.ProblemListItems
            .Where(p => p.OrthoCaseId == id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.CreatedAt)
            .Select(p => new { p.Id, p.Category, p.Description, p.Severity, p.SortOrder })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("{id:guid}/problem-list")]
    public async Task<IActionResult> AddProblemItem(Guid id, [FromBody] AddProblemItemRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var maxOrder = await db.ProblemListItems.Where(p => p.OrthoCaseId == id).MaxAsync(p => (int?)p.SortOrder) ?? 0;
        var item = new ProblemListItem
        {
            OrthoCaseId = id,
            Category    = req.Category,
            Description = req.Description,
            Severity    = req.Severity,
            SortOrder   = maxOrder + 1,
        };
        db.ProblemListItems.Add(item);
        await db.SaveChangesAsync();
        return Ok(new { item.Id, item.Category, item.Description, item.Severity, item.SortOrder });
    }

    [HttpDelete("{id:guid}/problem-list/{itemId:guid}")]
    public async Task<IActionResult> DeleteProblemItem(Guid id, Guid itemId)
    {
        var item = await db.ProblemListItems.FirstOrDefaultAsync(p => p.Id == itemId && p.OrthoCaseId == id);
        if (item is null) return NotFound();
        db.ProblemListItems.Remove(item);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم الحذف" });
    }

    // ─── Treatment Plan ──────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/treatment-plan")]
    public async Task<IActionResult> GetTreatmentPlan(Guid id)
    {
        var plan = await db.TreatmentPlans
            .Include(p => p.ApprovedByDoctor)
            .Where(p => p.OrthoCaseId == id)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
        if (plan is null) return Ok(null);
        return Ok(new
        {
            plan.Id,
            plan.PlanVersion,
            plan.IsApproved,
            plan.ApplianceType,
            plan.BracketSystem,
            plan.InitialWire,
            plan.ExtractionPlan,
            plan.AnchoragePlan,
            plan.UseTads,
            plan.UseElastics,
            plan.ExpectedDurationMonths,
            plan.RetentionPlan,
            plan.TreatmentGoals,
            plan.RisksLimitations,
            ApprovedByName = plan.ApprovedByDoctor?.Name,
            ApprovedAt     = plan.ApprovedAt?.ToString("yyyy-MM-dd"),
        });
    }

    [HttpPut("{id:guid}/treatment-plan")]
    public async Task<IActionResult> UpsertTreatmentPlan(Guid id, [FromBody] UpsertTreatmentPlanRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.TreatmentPlans.Where(p => p.OrthoCaseId == id).OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync();
        if (existing is null)
        {
            existing = new TreatmentPlan { OrthoCaseId = id };
            db.TreatmentPlans.Add(existing);
        }

        existing.ApplianceType          = req.ApplianceType;
        existing.BracketSystem          = req.BracketSystem;
        existing.InitialWire            = req.InitialWire;
        existing.ExtractionPlan         = req.ExtractionPlan;
        existing.AnchoragePlan          = req.AnchoragePlan;
        existing.UseTads                = req.UseTads;
        existing.UseElastics            = req.UseElastics;
        existing.ExpectedDurationMonths = req.ExpectedDurationMonths;
        existing.RetentionPlan          = req.RetentionPlan;
        existing.TreatmentGoals         = req.TreatmentGoals;
        existing.RisksLimitations       = req.RisksLimitations;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ خطة العلاج" });
    }

    // ─── Extraction Decision ─────────────────────────────────────────────────────

    [HttpPatch("{id:guid}/treatment-plan/approve")]
    public async Task<IActionResult> ApproveTreatmentPlan(Guid id)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var plan = await db.TreatmentPlans
            .Where(p => p.OrthoCaseId == id)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync();
        if (plan is null) return NotFound(new { message = "خطة العلاج غير موجودة" });

        plan.IsApproved = true;
        plan.ApprovedAt = DateTime.UtcNow;
        plan.ApprovedBy = orthoCase.DoctorId;
        await db.SaveChangesAsync();

        return Ok(new
        {
            plan.Id,
            plan.PlanVersion,
            plan.IsApproved,
            plan.ApplianceType,
            plan.BracketSystem,
            plan.InitialWire,
            plan.ExtractionPlan,
            plan.AnchoragePlan,
            plan.UseTads,
            plan.UseElastics,
            plan.ExpectedDurationMonths,
            plan.RetentionPlan,
            plan.TreatmentGoals,
            plan.RisksLimitations,
            ApprovedAt = plan.ApprovedAt?.ToString("yyyy-MM-dd")
        });
    }

    [HttpGet("{id:guid}/extraction-decision")]
    public async Task<IActionResult> GetExtractionDecision(Guid id)
    {
        var decision = await db.ExtractionDecisions
            .Include(e => e.Doctor)
            .Where(e => e.OrthoCaseId == id)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();
        if (decision is null) return Ok(null);
        return Ok(new {
            decision.Id,
            decision.Decision,
            decision.DoctorNotes,
            decision.AiRecommendation,
            DecidedByName = decision.Doctor?.Name,
            DecidedAt     = decision.DecidedAt?.ToString("yyyy-MM-dd"),
        });
    }

    [HttpPut("{id:guid}/extraction-decision")]
    public async Task<IActionResult> UpsertExtractionDecision(Guid id, [FromBody] UpsertExtractionDecisionRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.ExtractionDecisions.Where(e => e.OrthoCaseId == id).OrderByDescending(e => e.CreatedAt).FirstOrDefaultAsync();
        if (existing is null)
        {
            existing = new ExtractionDecision { OrthoCaseId = id };
            db.ExtractionDecisions.Add(existing);
        }
        existing.Decision    = req.Decision;
        existing.DoctorNotes = req.DoctorNotes;
        existing.DecidedAt   = DateTime.UtcNow;

        // Mirror to orthoCase for quick access
        orthoCase.ExtractionDecisionValue = req.Decision;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ قرار الخلع" });
    }

    // ─── Retention Records ─────────────────────────────────────────────────────

    [HttpGet("{id:guid}/retention")]
    public async Task<IActionResult> GetRetention(Guid id)
    {
        var record = await db.RetentionRecords
            .Include(r => r.Visits)
            .Where(r => r.OrthoCaseId == id)
            .FirstOrDefaultAsync();
        if (record is null) return Ok(null);
        return Ok(new
        {
            record.Id,
            DebondDate   = record.DebondDate?.ToString("yyyy-MM-dd"),
            record.UpperRetainer,
            record.LowerRetainer,
            record.Instructions,
            record.Status,
            Visits = record.Visits
                .OrderBy(v => v.VisitDate)
                .Select(v => new
                {
                    v.Id,
                    VisitDate     = v.VisitDate?.ToString("yyyy-MM-dd"),
                    v.Period,
                    v.ToothStability,
                    v.RetainerStatus,
                    v.Notes,
                })
        });
    }

    [HttpPut("{id:guid}/retention")]
    public async Task<IActionResult> UpsertRetention(Guid id, [FromBody] UpsertRetentionRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.RetentionRecords.FirstOrDefaultAsync(r => r.OrthoCaseId == id);
        if (existing is null)
        {
            existing = new RetentionRecord { OrthoCaseId = id };
            db.RetentionRecords.Add(existing);
        }

        existing.DebondDate    = req.DebondDate != null ? DateOnly.Parse(req.DebondDate) : null;
        existing.UpperRetainer = req.UpperRetainer;
        existing.LowerRetainer = req.LowerRetainer;
        existing.Instructions  = req.Instructions;
        existing.Status        = req.Status ?? existing.Status;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ سجل الاحتفاظ" });
    }

    [HttpPost("{id:guid}/retention/visits")]
    public async Task<IActionResult> AddRetentionVisit(Guid id, [FromBody] CreateRetentionVisitRequest req)
    {
        var retention = await db.RetentionRecords.FirstOrDefaultAsync(r => r.OrthoCaseId == id);
        if (retention is null) return NotFound(new { message = "سجل الاحتفاظ غير موجود — أنشئ سجل الاحتفاظ أولاً" });

        var visit = new RetentionVisit
        {
            RetentionRecordId = retention.Id,
            VisitDate         = req.VisitDate != null ? DateOnly.Parse(req.VisitDate) : DateOnly.FromDateTime(DateTime.Today),
            Period            = req.Period,
            ToothStability    = req.ToothStability,
            RetainerStatus    = req.RetainerStatus,
            Notes             = req.Notes,
        };
        db.RetentionVisits.Add(visit);
        await db.SaveChangesAsync();
        return Ok(new
        {
            visit.Id,
            VisitDate     = visit.VisitDate?.ToString("yyyy-MM-dd"),
            visit.Period,
            visit.ToothStability,
            visit.RetainerStatus,
            visit.Notes,
        });
    }

    [HttpGet("{id:guid}/retention/visits")]
    public async Task<IActionResult> GetRetentionVisits(Guid id)
    {
        var visits = await db.RetentionVisits
            .Where(v => v.RetentionRecord.OrthoCaseId == id)
            .OrderByDescending(v => v.VisitDate)
            .Select(v => new
            {
                v.Id,
                VisitDate     = v.VisitDate != null ? v.VisitDate.Value.ToString("yyyy-MM-dd") : null,
                v.Period,
                v.ToothStability,
                v.RetainerStatus,
                v.Notes,
            })
            .ToListAsync();
        return Ok(visits);
    }

    // ─── Diagnosis Summary ──────────────────────────────────────────────────────

    [HttpGet("{id:guid}/diagnosis")]
    public async Task<IActionResult> GetDiagnosis(Guid id)
    {
        var diagnosis = await db.OrthoDiagnoses
            .Where(d => d.OrthoCaseId == id)
            .FirstOrDefaultAsync();

        if (diagnosis is not null)
        {
            return Ok(new
            {
                diagnosis.Id,
                diagnosis.SkeletalClassification,
                diagnosis.DentalClassification,
                diagnosis.FacialPattern,
                diagnosis.ANB,
                diagnosis.Wits,
                diagnosis.FMA,
                diagnosis.SNA,
                diagnosis.SNB,
                diagnosis.IMPA,
                diagnosis.Summary,
            });
        }

        // Compute a summary from ClinicalExam, ProblemList, and CephAnalysis measurements
        var orthoCase = await db.OrthoCases
            .Include(c => c.ClinicalExam)
            .Include(c => c.ProblemList)
            .Include(c => c.CephAnalyses)
                .ThenInclude(a => a.Measurements)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        // Derive skeletal classification from the latest ceph measurements
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

        return Ok(new
        {
            Id = (Guid?)null,
            SkeletalClassification = skeletalClass,
            DentalClassification = dentalClass,
            FacialPattern = facialPattern,
            ANB = anbValue,
            Wits = witsValue,
            FMA = fmaValue,
            SNA = snaValue,
            SNB = snbValue,
            IMPA = impaValue,
            Summary = problemSummary,
        });
    }

    [HttpPut("{id:guid}/diagnosis")]
    public async Task<IActionResult> UpsertDiagnosis(Guid id, [FromBody] UpsertDiagnosisRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var existing = await db.OrthoDiagnoses.FirstOrDefaultAsync(d => d.OrthoCaseId == id);
        if (existing is null)
        {
            existing = new OrthoDiagnosis { OrthoCaseId = id };
            db.OrthoDiagnoses.Add(existing);
        }

        existing.SkeletalClassification = req.SkeletalClassification;
        existing.DentalClassification   = req.DentalClassification;
        existing.FacialPattern          = req.FacialPattern;
        existing.ANB                    = req.ANB;
        existing.Wits                   = req.Wits;
        existing.FMA                    = req.FMA;
        existing.SNA                    = req.SNA;
        existing.SNB                    = req.SNB;
        existing.IMPA                   = req.IMPA;
        existing.Summary                = req.Summary;

        await db.SaveChangesAsync();
        return Ok(new { existing.Id, message = "تم حفظ التشخيص" });
    }

    // ─── Clinical Photos ────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/photos")]
    public async Task<IActionResult> AddPhoto(Guid id, [FromBody] AddOrthoPhotoRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var maxOrder = await db.OrthoClinicalPhotos
            .Where(p => p.OrthoCaseId == id)
            .MaxAsync(p => (int?)p.SortOrder) ?? 0;

        var photo = new OrthoClinicalPhoto
        {
            OrthoCaseId = id,
            PhotoUrl    = req.PhotoUrl,
            PhotoType   = req.PhotoType ?? "Intraoral",
            Caption     = req.Caption,
            TakenAt     = req.TakenAt ?? DateTime.UtcNow,
            SortOrder   = req.SortOrder ?? (maxOrder + 1),
        };
        db.OrthoClinicalPhotos.Add(photo);
        await db.SaveChangesAsync();

        return Ok(new
        {
            photo.Id,
            photo.PhotoUrl,
            photo.PhotoType,
            photo.Caption,
            TakenAt   = photo.TakenAt.ToString("yyyy-MM-dd"),
            photo.SortOrder,
        });
    }

    [HttpGet("{id:guid}/photos")]
    public async Task<IActionResult> GetPhotos(Guid id)
    {
        var photos = await db.OrthoClinicalPhotos
            .Where(p => p.OrthoCaseId == id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.TakenAt)
            .Select(p => new
            {
                p.Id,
                p.PhotoUrl,
                p.PhotoType,
                p.Caption,
                TakenAt   = p.TakenAt.ToString("yyyy-MM-dd"),
                p.SortOrder,
            })
            .ToListAsync();
        return Ok(photos);
    }

    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    public async Task<IActionResult> DeletePhoto(Guid id, Guid photoId)
    {
        var photo = await db.OrthoClinicalPhotos
            .FirstOrDefaultAsync(p => p.Id == photoId && p.OrthoCaseId == id);
        if (photo is null) return NotFound(new { message = "الصورة غير موجودة" });

        db.OrthoClinicalPhotos.Remove(photo);
        await db.SaveChangesAsync();
        return Ok(new { message = "تم حذف الصورة" });
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────────────

public class UpdateStageRequest
{
    public string Status { get; set; } = string.Empty;
}

public sealed class AddProblemItemRequest
{
    public string Category    { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Severity   { get; init; }
}

public sealed class UpsertExtractionDecisionRequest
{
    public string? Decision    { get; init; }
    public string? DoctorNotes { get; init; }
}

public sealed class UpsertTreatmentPlanRequest
{
    public string? ApplianceType          { get; init; }
    public string? BracketSystem          { get; init; }
    public string? InitialWire            { get; init; }
    public string? ExtractionPlan         { get; init; }
    public string? AnchoragePlan          { get; init; }
    public bool UseTads                   { get; init; }
    public bool UseElastics               { get; init; }
    public int? ExpectedDurationMonths    { get; init; }
    public string? RetentionPlan          { get; init; }
    public string? TreatmentGoals         { get; init; }
    public string? RisksLimitations       { get; init; }
}

public sealed class UpsertRetentionRequest
{
    public string? DebondDate    { get; init; }
    public string? UpperRetainer { get; init; }
    public string? LowerRetainer { get; init; }
    public string? Instructions  { get; init; }
    public string? Status        { get; init; }
}

public sealed class CreateRetentionVisitRequest
{
    public string? VisitDate      { get; init; }
    public string? Period         { get; init; }
    public string? ToothStability { get; init; }
    public string? RetainerStatus { get; init; }
    public string? Notes          { get; init; }
}

public sealed class UpsertDiagnosisRequest
{
    public string? SkeletalClassification { get; init; }
    public string? DentalClassification   { get; init; }
    public string? FacialPattern          { get; init; }
    public decimal? ANB                   { get; init; }
    public decimal? Wits                  { get; init; }
    public decimal? FMA                   { get; init; }
    public decimal? SNA                   { get; init; }
    public decimal? SNB                   { get; init; }
    public decimal? IMPA                  { get; init; }
    public string? Summary                { get; init; }
}

public sealed class AddOrthoPhotoRequest
{
    public string PhotoUrl   { get; init; } = string.Empty;
    public string? PhotoType { get; init; }
    public string? Caption   { get; init; }
    public DateTime? TakenAt { get; init; }
    public int? SortOrder    { get; init; }
}
