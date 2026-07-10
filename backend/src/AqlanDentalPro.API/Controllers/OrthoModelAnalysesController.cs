using System.Text.Json;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ortho-cases/{orthoCaseId:guid}/model-analyses")]
[Authorize(Policy = "OrthoAccess")]
public class OrthoModelAnalysesController(
    AppDbContext db,
    IPatientAccessService patientAccess,
    ICurrentUserService currentUser) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> List(Guid orthoCaseId)
    {
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        var rows = await db.ModelAnalyses
            .AsNoTracking()
            .Where(x => x.OrthoCaseId == orthoCaseId)
            .OrderByDescending(x => x.AnalysisDate)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(rows.Select(ToResponse));
    }

    [HttpGet("latest")]
    public async Task<IActionResult> Latest(Guid orthoCaseId)
    {
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        var row = await db.ModelAnalyses
            .AsNoTracking()
            .Where(x => x.OrthoCaseId == orthoCaseId)
            .OrderByDescending(x => x.AnalysisDate)
            .ThenByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();

        return row is null ? NoContent() : Ok(ToResponse(row));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid orthoCaseId, Guid id)
    {
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        var row = await db.ModelAnalyses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.OrthoCaseId == orthoCaseId);

        return row is null
            ? NotFound(new { message = "تحليل النماذج غير موجود" })
            : Ok(ToResponse(row));
    }

    // GET api/ortho-cases/{orthoCaseId}/model-analyses/latest/report/pdf
    [HttpGet("latest/report/pdf")]
    public async Task<IActionResult> GetLatestReportPdf(
        Guid orthoCaseId,
        [FromServices] AqlanDentalPro.API.Services.OrthoModelAnalysisReportPdfGenerator generator,
        [FromServices] ILogger<OrthoModelAnalysesController> logger)
    {
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        try
        {
            var pdf = await generator.GenerateLatestAsync(orthoCaseId);
            return File(pdf, "application/pdf", $"model-analysis-{orthoCaseId}.pdf");
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "لا يوجد تحليل نماذج لهذه الحالة" });
        }
        catch (Exception ex)
        {
            // Never expose exception details in the HTTP response — log only.
            logger.LogError(ex, "Failed to generate model analysis report PDF for case {CaseId}", orthoCaseId);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء تقرير تحليل النماذج" });
        }
    }

    // GET api/ortho-cases/{orthoCaseId}/model-analyses/{id}/report/pdf
    [HttpGet("{id:guid}/report/pdf")]
    public async Task<IActionResult> GetReportPdf(
        Guid orthoCaseId,
        Guid id,
        [FromServices] AqlanDentalPro.API.Services.OrthoModelAnalysisReportPdfGenerator generator,
        [FromServices] ILogger<OrthoModelAnalysesController> logger)
    {
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        // Ensure the analysis belongs to the accessed case before rendering.
        var exists = await db.ModelAnalyses.AsNoTracking()
            .AnyAsync(m => m.Id == id && m.OrthoCaseId == orthoCaseId);
        if (!exists) return NotFound(new { message = "تحليل النماذج غير موجود" });

        try
        {
            var pdf = await generator.GenerateAsync(id);
            return File(pdf, "application/pdf", $"model-analysis-{id}.pdf");
        }
        catch (ArgumentException)
        {
            return NotFound(new { message = "تحليل النماذج غير موجود" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate model analysis report PDF {AnalysisId}", id);
            return StatusCode(500, new { message = "حدث خطأ غير متوقع أثناء إنشاء تقرير تحليل النماذج" });
        }
    }

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        Guid orthoCaseId,
        [FromBody] DentalModelAnalysisInput input)
    {
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        var validationError = Validate(input);
        return validationError is null
            ? Ok(DentalModelAnalysisCalculator.Calculate(input))
            : BadRequest(new { message = validationError });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid orthoCaseId,
        [FromBody] SaveDentalModelAnalysisRequest request)
    {
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        var validationError = Validate(request.Inputs);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var result = DentalModelAnalysisCalculator.Calculate(request.Inputs);
        var row = new ModelAnalysis
        {
            OrthoCaseId = orthoCaseId,
            AnalysisDate = request.AnalysisDate ?? ClinicTimeProvider.ClinicToday(),
            DentitionStage = NormalizeDentitionStage(request.DentitionStage),
            InputDataJson = JsonSerializer.Serialize(request.Inputs, JsonOptions),
            ResultDataJson = JsonSerializer.Serialize(result, JsonOptions),
            Notes = request.Notes?.Trim(),
        };
        ApplySummaries(row, request.Inputs, result);

        db.ModelAnalyses.Add(row);
        await db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetById),
            new { orthoCaseId, id = row.Id },
            ToResponse(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid orthoCaseId,
        Guid id,
        [FromBody] SaveDentalModelAnalysisRequest request)
    {
        var accessError = await GetCaseAccessErrorAsync(orthoCaseId);
        if (accessError is not null) return accessError;

        var row = await db.ModelAnalyses
            .FirstOrDefaultAsync(x => x.Id == id && x.OrthoCaseId == orthoCaseId);
        if (row is null)
            return NotFound(new { message = "تحليل النماذج غير موجود" });
        if (row.ApprovedAt.HasValue)
            return Conflict(new
            {
                message = "التحليل معتمد ولا يمكن تعديله. أنشئ نسخة جديدة لتوثيق قياسات محدثة.",
            });

        var validationError = Validate(request.Inputs);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var result = DentalModelAnalysisCalculator.Calculate(request.Inputs);
        row.AnalysisDate = request.AnalysisDate ?? row.AnalysisDate;
        row.DentitionStage = NormalizeDentitionStage(request.DentitionStage);
        row.InputDataJson = JsonSerializer.Serialize(request.Inputs, JsonOptions);
        row.ResultDataJson = JsonSerializer.Serialize(result, JsonOptions);
        row.Notes = request.Notes?.Trim();
        ApplySummaries(row, request.Inputs, result);

        await db.SaveChangesAsync();
        return Ok(ToResponse(row));
    }

    [HttpPatch("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid orthoCaseId, Guid id)
    {
        var orthoCase = await db.OrthoCases
            .FirstOrDefaultAsync(x => x.Id == orthoCaseId && x.IsActive);
        if (orthoCase is null)
            return NotFound(new { message = "حالة التقويم غير موجودة" });
        if (!await patientAccess.CanAccessPatientAsync(orthoCase.PatientId))
            return Forbid();

        var userId = currentUser.UserId;
        if (userId is null)
            return Unauthorized(new { message = "يلزم تسجيل الدخول لاعتماد التحليل" });

        var isAssignedOrthodontist = orthoCase.DoctorId.HasValue &&
            await db.Doctors.AnyAsync(x =>
                x.Id == orthoCase.DoctorId.Value &&
                x.UserId == userId.Value &&
                x.IsActive);

        if (!currentUser.IsAdmin && !isAssignedOrthodontist)
            return Forbid();

        var row = await db.ModelAnalyses
            .FirstOrDefaultAsync(x => x.Id == id && x.OrthoCaseId == orthoCaseId);
        if (row is null)
            return NotFound(new { message = "تحليل النماذج غير موجود" });
        if (row.ApprovedAt.HasValue)
            return Conflict(new { message = "تم اعتماد هذا التحليل مسبقًا" });

        Guid? approvingDoctorId = null;
        if (isAssignedOrthodontist)
        {
            approvingDoctorId = orthoCase.DoctorId;
        }
        else if (currentUser.IsAdmin)
        {
            approvingDoctorId = await db.Doctors
                .Where(x => x.UserId == userId.Value && x.IsActive)
                .Select(x => (Guid?)x.Id)
                .FirstOrDefaultAsync();
        }

        row.ApprovedBy = approvingDoctorId;
        row.ApprovedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        return Ok(ToResponse(row));
    }

    private async Task<IActionResult?> GetCaseAccessErrorAsync(Guid orthoCaseId)
    {
        var orthoCase = await db.OrthoCases
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orthoCaseId && x.IsActive);
        if (orthoCase is null)
            return NotFound(new { message = "حالة التقويم غير موجودة" });

        return await patientAccess.CanAccessPatientAsync(orthoCase.PatientId)
            ? null
            : Forbid();
    }

    private static string? Validate(DentalModelAnalysisInput input)
    {
        if (input.ToothWidths is null)
            return "بيانات قياسات الأسنان مطلوبة";
        if (input.ToothWidths.Any(x => x.Value is < 0 or > 20))
            return "قياس عرض السن يجب أن يكون بين 0 و20 مم";
        if (input.HuckabaTeeth?.Any(x =>
                x.RadiographicUneruptedWidth is <= 0 or > 30 ||
                x.ActualReferenceWidth is <= 0 or > 30 ||
                x.RadiographicReferenceWidth is <= 0 or > 30) == true)
            return "قياسات Huckaba يجب أن تكون موجبة وأقل من 30 مم";
        return null;
    }

    private static string NormalizeDentitionStage(string? value)
        => value is "Mixed" or "Permanent" ? value : "Permanent";

    private static void ApplySummaries(
        ModelAnalysis row,
        DentalModelAnalysisInput input,
        DentalModelAnalysisResult result)
    {
        row.BoltonOverall = result.Bolton?.OverallRatio;
        row.BoltonAnterior = result.Bolton?.AnteriorRatio;
        row.UpperSum12 = result.UpperArch?.Required;
        row.LowerSum12 = result.LowerArch?.Required;
        row.UpperArchLength = input.UpperAvailableSpace;
        row.LowerArchLength = input.LowerAvailableSpace;
        row.UpperAld = result.UpperArch?.Discrepancy;
        row.LowerAld = result.LowerArch?.Discrepancy;
        row.PontIndex = result.Pont?.IncisorSum;
    }

    private static object ToResponse(ModelAnalysis row)
        => new
        {
            row.Id,
            row.OrthoCaseId,
            row.AnalysisDate,
            row.DentitionStage,
            row.AnalysisVersion,
            Inputs = Deserialize<DentalModelAnalysisInput>(row.InputDataJson),
            Results = Deserialize<DentalModelAnalysisResult>(row.ResultDataJson),
            row.ApprovedBy,
            row.ApprovedAt,
            row.Notes,
            row.CreatedAt,
            row.UpdatedAt,
        };

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}

public sealed record SaveDentalModelAnalysisRequest(
    DateOnly? AnalysisDate,
    string? DentitionStage,
    DentalModelAnalysisInput Inputs,
    string? Notes);
