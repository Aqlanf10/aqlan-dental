using AqlanDentalPro.Application.DTOs.Ortho;
using AqlanDentalPro.Application.Exceptions;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ortho-cases/{id:guid}/ai")]
[Authorize(Policy = "OrthoAccess")]
public sealed class OrthoCaseAiController(
    AppDbContext db,
    IPatientAccessService patientAccess,
    OrthoCaseDraftService draftService,
    ILogger<OrthoCaseAiController> logger) : ControllerBase
{
    [HttpPost("clinical-draft")]
    public async Task<IActionResult> GenerateClinicalDraft(
        Guid id,
        [FromBody] OrthoCaseDraftRequestDto request,
        CancellationToken ct)
    {
        var accessError = await GetCaseAccessErrorAsync(id, ct);
        if (accessError is not null) return accessError;

        try
        {
            var result = await draftService.GenerateAsync(id, request.Section, ct);
            return result is null
                ? NotFound(new { message = "الحالة التقويمية غير موجودة" })
                : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (CephAiUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (CephAiLimitReachedException ex)
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, new { message = ex.Message });
        }
        catch (CephAiUpstreamException)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = CephAiDraftService.UpstreamFailureMessageAr });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate orthodontic case clinical draft for case {CaseId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "تعذر توليد المسودة السريرية حالياً" });
        }
    }

    private async Task<IActionResult?> GetCaseAccessErrorAsync(Guid orthoCaseId, CancellationToken ct)
    {
        var patientId = await db.OrthoCases
            .AsNoTracking()
            .Where(c => c.Id == orthoCaseId && c.IsActive)
            .Select(c => (Guid?)c.PatientId)
            .FirstOrDefaultAsync(ct);

        if (!patientId.HasValue)
            return NotFound(new { message = "الحالة التقويمية غير موجودة" });

        return await patientAccess.CanAccessPatientAsync(patientId.Value) ? null : Forbid();
    }
}
