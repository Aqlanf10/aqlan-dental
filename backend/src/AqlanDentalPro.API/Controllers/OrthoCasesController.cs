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
[Authorize]
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
