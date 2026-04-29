using AqlanDentalPro.Application.DTOs.General;
using AqlanDentalPro.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

public sealed class CreatePerioAssessmentRequestValidator : AbstractValidator<CreatePerioAssessmentRequest>
{
    public CreatePerioAssessmentRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .NotEmpty().WithMessage("المريض مطلوب");

        RuleFor(x => x.AssessmentDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق التاريخ غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.AssessmentDate));

        RuleFor(x => x.PerioStage)
            .MaximumLength(50).WithMessage("مرحلة أمراض اللثة يجب ألا تتجاوز 50 حرف")
            .When(x => !string.IsNullOrWhiteSpace(x.PerioStage));

        RuleFor(x => x.BoneLossPattern)
            .Must(p => p == null || p == "horizontal" || p == "vertical" || p == "generalized")
            .WithMessage("نمط فقدان العظم غير صالح (horizontal/vertical/generalized)")
            .When(x => !string.IsNullOrWhiteSpace(x.BoneLossPattern));

        RuleFor(x => x.AvgPocketDepth)
            .InclusiveBetween(0m, 15m).WithMessage("متوسط عمق الجيب يجب أن يكون بين 0 و 15 مم")
            .When(x => x.AvgPocketDepth.HasValue);

        RuleFor(x => x.PlaqueIndex)
            .InclusiveBetween(0m, 3m).WithMessage("مؤشر اللويحة يجب أن يكون بين 0 و 3")
            .When(x => x.PlaqueIndex.HasValue);

        RuleFor(x => x.GingivalIndex)
            .InclusiveBetween(0m, 3m).WithMessage("مؤشر اللثة يجب أن يكون بين 0 و 3")
            .When(x => x.GingivalIndex.HasValue);
    }
}

public sealed class UpdatePerioAssessmentRequestValidator : AbstractValidator<UpdatePerioAssessmentRequest>
{
    public UpdatePerioAssessmentRequestValidator()
    {
        RuleFor(x => x.AssessmentDate)
            .Must(d => DateOnly.TryParse(d, out _)).WithMessage("تنسيق التاريخ غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.AssessmentDate));

        RuleFor(x => x.BoneLossPattern)
            .Must(p => p == "horizontal" || p == "vertical" || p == "generalized")
            .WithMessage("نمط فقدان العظم غير صالح")
            .When(x => !string.IsNullOrWhiteSpace(x.BoneLossPattern));

        RuleFor(x => x.AvgPocketDepth)
            .InclusiveBetween(0m, 15m).WithMessage("متوسط عمق الجيب يجب أن يكون بين 0 و 15 مم")
            .When(x => x.AvgPocketDepth.HasValue);

        RuleFor(x => x.PlaqueIndex)
            .InclusiveBetween(0m, 3m).WithMessage("مؤشر اللويحة يجب أن يكون بين 0 و 3")
            .When(x => x.PlaqueIndex.HasValue);

        RuleFor(x => x.GingivalIndex)
            .InclusiveBetween(0m, 3m).WithMessage("مؤشر اللثة يجب أن يكون بين 0 و 3")
            .When(x => x.GingivalIndex.HasValue);
    }
}

public sealed class UpdateGeneralTreatmentRequestValidator : AbstractValidator<UpdateGeneralTreatmentRequest>
{
    public UpdateGeneralTreatmentRequestValidator()
    {
        RuleFor(x => x.TreatmentType)
            .NotEmpty().WithMessage("نوع العلاج مطلوب")
            .When(x => x.TreatmentType is not null);
    }
}

[ApiController]
[Route("api")]
[Authorize(Policy = "GeneralAccess")]
public class GeneralController(GeneralService service) : ControllerBase
{
    // ── Dental Chart ─────────────────────────────────────────────────────────────

    [HttpGet("dental-chart/{patientId:guid}")]
    public async Task<IActionResult> GetChart(Guid patientId)
    {
        var result = await service.GetOrCreateChartAsync(patientId);
        return Ok(result);
    }

    [HttpPut("dental-chart/{patientId:guid}/teeth")]
    public async Task<IActionResult> UpdateTooth(Guid patientId, [FromBody] UpdateToothRequest req)
    {
        var result = await service.UpdateToothAsync(patientId, req);
        return Ok(result);
    }

    // ── General Treatments ──────────────────────────────────────────────────────

    [HttpGet("general-treatments/{patientId:guid}")]
    public async Task<IActionResult> GetTreatments(Guid patientId)
    {
        var result = await service.GetTreatmentsAsync(patientId);
        return Ok(result);
    }

    [HttpPost("general-treatments")]
    public async Task<IActionResult> CreateTreatment([FromBody] CreateGeneralTreatmentRequest req)
    {
        var result = await service.CreateTreatmentAsync(req);
        return Ok(result);
    }

    [HttpPut("general-treatments/{id:guid}")]
    public async Task<IActionResult> UpdateTreatment(Guid id, [FromBody] UpdateGeneralTreatmentRequest req)
    {
        try
        {
            var result = await service.UpdateTreatmentAsync(id, req);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("general-treatments/{id:guid}")]
    public async Task<IActionResult> DeleteTreatment(Guid id)
    {
        try
        {
            await service.DeleteTreatmentAsync(id);
            return Ok(new { message = "تم حذف العلاج بنجاح" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("general/recent-treatments")]
    public async Task<IActionResult> GetRecentTreatments([FromQuery] int limit = 20)
    {
        var result = await service.GetRecentTreatmentsAsync(limit);
        return Ok(result);
    }

    // ── Perio Assessment ────────────────────────────────────────────────────────

    [HttpGet("perio/{patientId:guid}")]
    public async Task<IActionResult> GetPerioAssessment(Guid patientId)
    {
        var result = await service.GetPerioAssessmentAsync(patientId);
        return Ok(result);
    }

    [HttpGet("perio/{patientId:guid}/history")]
    public async Task<IActionResult> GetPerioHistory(Guid patientId)
    {
        var result = await service.GetPerioHistoryAsync(patientId);
        return Ok(result);
    }

    [HttpPost("perio/{patientId:guid}")]
    public async Task<IActionResult> CreatePerioAssessment(
        Guid patientId,
        [FromBody] CreatePerioAssessmentRequest req)
    {
        req.PatientId = patientId;
        var result = await service.CreatePerioAssessmentAsync(req);
        return Ok(result);
    }

    [HttpPut("perio/{id:guid}")]
    public async Task<IActionResult> UpdatePerioAssessment(
        Guid id,
        [FromBody] UpdatePerioAssessmentRequest req)
    {
        try
        {
            var result = await service.UpdatePerioAssessmentAsync(id, req);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpDelete("perio/{id:guid}")]
    public async Task<IActionResult> DeletePerioAssessment(Guid id)
    {
        try
        {
            await service.DeletePerioAssessmentAsync(id);
            return Ok(new { message = "تم حذف تقييم اللثة بنجاح" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
