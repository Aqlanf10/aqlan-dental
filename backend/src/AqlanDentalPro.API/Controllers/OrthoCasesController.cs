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
            plan.PlanLabel,
            plan.IsSelected,
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
        var serviceReq = new UpsertTreatmentPlanServiceRequest
        {
            PlanLabel = req.PlanLabel,
            ApplianceType = req.ApplianceType,
            BracketSystem = req.BracketSystem,
            InitialWire = req.InitialWire,
            ExtractionPlan = req.ExtractionPlan,
            AnchoragePlan = req.AnchoragePlan,
            UseTads = req.UseTads,
            UseElastics = req.UseElastics,
            ExpectedDurationMonths = req.ExpectedDurationMonths,
            RetentionPlan = req.RetentionPlan,
            TreatmentGoals = req.TreatmentGoals,
            RisksLimitations = req.RisksLimitations
        };

        var result = await service.SaveTreatmentPlanAsync(id, serviceReq);
        return Ok(result);
    }

    [HttpGet("{id:guid}/treatment-plans")]
    public async Task<IActionResult> GetAllTreatmentPlans(Guid id)
    {
        var result = await service.GetAllTreatmentPlansAsync(id);
        return Ok(result);
    }

    [HttpPut("{id:guid}/treatment-plan/{planId:guid}/select")]
    public async Task<IActionResult> SelectTreatmentPlan(Guid id, Guid planId)
    {
        var result = await service.SelectTreatmentPlanAsync(id, planId);
        if (result is null) return NotFound(new { message = "خطة العلاج غير موجودة" });
        return Ok(result);
    }

    // ─── Extraction Decision ─────────────────────────────────────────────────────

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

    // ─── Model Analysis ─────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/model-analysis")]
    public async Task<IActionResult> GetModelAnalysis(Guid id)
    {
        var result = await service.GetModelAnalysisAsync(id);
        return Ok(result);
    }

    [HttpPut("{id:guid}/model-analysis")]
    public async Task<IActionResult> SaveModelAnalysis(Guid id, [FromBody] SaveModelAnalysisRequest req)
    {
        var result = await service.SaveModelAnalysisAsync(id, req);
        return Ok(result);
    }

    [HttpPost("{id:guid}/bolton-calculation")]
    public async Task<IActionResult> CalculateBolton(Guid id, [FromBody] BoltonCalculationRequest req)
    {
        var result = await service.CalculateBoltonAsync(id, req);
        return Ok(result);
    }

    // ─── Retention ──────────────────────────────────────────────────────────────

    [HttpGet("{id:guid}/retention")]
    public async Task<IActionResult> GetRetention(Guid id)
    {
        var retention = await db.RetentionRecords
            .Where(r => r.OrthoCaseId == id)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (retention is null) return Ok(null);

        var visits = await db.RetentionVisits
            .Where(v => v.RetentionRecordId == retention.Id)
            .OrderByDescending(v => v.VisitDate)
            .Select(v => new
            {
                v.Id,
                VisitDate = v.VisitDate.HasValue ? v.VisitDate.Value.ToString("yyyy-MM-dd") : null,
                v.Period,
                v.ToothStability,
                v.RetainerStatus,
                v.Notes
            })
            .ToListAsync();

        return Ok(new
        {
            retention.Id,
            DebondDate = retention.DebondDate?.ToString("yyyy-MM-dd"),
            retention.UpperRetainer,
            retention.LowerRetainer,
            retention.Instructions,
            retention.Status,
            Visits = visits
        });
    }

    [HttpPost("{id:guid}/retention")]
    public async Task<IActionResult> CreateRetention(Guid id, [FromBody] CreateRetentionRequest req)
    {
        var orthoCase = await db.OrthoCases.FindAsync(id);
        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        // Check if retention already exists
        var existing = await db.RetentionRecords.AnyAsync(r => r.OrthoCaseId == id);
        if (existing) return Conflict(new { message = "يوجد سجل احتفاظ بالفعل" });

        var retention = new RetentionRecord
        {
            OrthoCaseId   = id,
            DebondDate    = req.DebondDate != null ? DateOnly.Parse(req.DebondDate) : null,
            UpperRetainer = req.UpperRetainer,
            LowerRetainer = req.LowerRetainer,
            Instructions  = req.Instructions,
            Status        = "active"
        };

        // Update ortho case status to retention
        orthoCase.Status = "retention";

        db.RetentionRecords.Add(retention);
        await db.SaveChangesAsync();

        return Ok(new { retention.Id, message = "تم إنشاء سجل الاحتفاظ" });
    }

    [HttpPut("{id:guid}/retention")]
    public async Task<IActionResult> UpdateRetention(Guid id, [FromBody] UpdateRetentionRequest req)
    {
        var retention = await db.RetentionRecords
            .FirstOrDefaultAsync(r => r.OrthoCaseId == id);
        if (retention is null) return NotFound(new { message = "سجل الاحتفاظ غير موجود" });

        if (req.DebondDate != null) retention.DebondDate = DateOnly.Parse(req.DebondDate);
        if (req.UpperRetainer != null) retention.UpperRetainer = req.UpperRetainer;
        if (req.LowerRetainer != null) retention.LowerRetainer = req.LowerRetainer;
        if (req.Instructions != null) retention.Instructions = req.Instructions;
        if (req.Status != null) retention.Status = req.Status;

        await db.SaveChangesAsync();
        return Ok(new { retention.Id, message = "تم تحديث سجل الاحتفاظ" });
    }

    [HttpPost("{id:guid}/retention/visits")]
    public async Task<IActionResult> AddRetentionVisit(Guid id, [FromBody] CreateRetentionVisitRequest req)
    {
        var retention = await db.RetentionRecords.FirstOrDefaultAsync(r => r.OrthoCaseId == id);
        if (retention is null) return NotFound(new { message = "سجل الاحتفاظ غير موجود" });

        var visit = new RetentionVisit
        {
            RetentionRecordId = retention.Id,
            VisitDate      = DateOnly.Parse(req.VisitDate),
            Period         = req.Period,
            ToothStability = req.ToothStability,
            RetainerStatus = req.RetainerStatus,
            Notes          = req.Notes
        };

        db.RetentionVisits.Add(visit);
        await db.SaveChangesAsync();

        return Ok(new { visit.Id, message = "تم إضافة زيارة الاحتفاظ" });
    }

    // ─── Debonding Summary ──────────────────────────────────────────────────────

    [HttpGet("{id:guid}/debonding-summary")]
    public async Task<IActionResult> GetDebondingSummary(Guid id)
    {
        var orthoCase = await db.OrthoCases
            .Include(c => c.Patient)
            .Include(c => c.Doctor)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (orthoCase is null) return NotFound(new { message = "الحالة غير موجودة" });

        var retention = await db.RetentionRecords
            .FirstOrDefaultAsync(r => r.OrthoCaseId == id);

        var totalVisits = await db.OrthoVisits.CountAsync(v => v.OrthoCaseId == id);
        var totalPayments = await db.Payments
            .Where(p => p.Contract != null && p.Contract.RelatedCaseId == id)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        return Ok(new
        {
            CaseNumber = orthoCase.CaseNumber,
            PatientName = orthoCase.Patient != null ? $"{orthoCase.Patient.FirstName} {orthoCase.Patient.LastName}" : null,
            DoctorName = orthoCase.Doctor?.Name,
            StartDate = orthoCase.StartDate?.ToString("yyyy-MM-dd"),
            DebondDate = retention?.DebondDate?.ToString("yyyy-MM-dd"),
            ApplianceType = orthoCase.ApplianceType,
            TotalVisits = totalVisits,
            TotalFee = orthoCase.TotalFee,
            TotalPaid = totalPayments,
            UpperRetainer = retention?.UpperRetainer,
            LowerRetainer = retention?.LowerRetainer,
            RetentionInstructions = retention?.Instructions,
        });
    }
}

public sealed class CreateRetentionRequest
{
    public string? DebondDate { get; init; }
    public string? UpperRetainer { get; init; }
    public string? LowerRetainer { get; init; }
    public string? Instructions { get; init; }
}

public sealed class UpdateRetentionRequest
{
    public string? DebondDate { get; init; }
    public string? UpperRetainer { get; init; }
    public string? LowerRetainer { get; init; }
    public string? Instructions { get; init; }
    public string? Status { get; init; }
}

public sealed class CreateRetentionVisitRequest
{
    public string VisitDate { get; init; } = string.Empty;
    public string? Period { get; init; }
    public string? ToothStability { get; init; }
    public string? RetainerStatus { get; init; }
    public string? Notes { get; init; }
}

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
    /// <summary>Plan label: "A" (default) or "B" (alternative)</summary>
    public string? PlanLabel              { get; init; }
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
