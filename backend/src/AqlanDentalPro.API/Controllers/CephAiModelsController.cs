using AqlanDentalPro.Application.DTOs.Ceph;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/ceph-ai-models")]
[Authorize(Policy = "AdminOnly")]
public sealed class CephAiModelsController(CephAiModelRegistryService registry) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await registry.GetRegistryAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterCephAiModelVersionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await registry.RegisterCandidateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "model.invalid-contract", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveCephAiModelVersionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await registry.ApproveAsync(id, request, cancellationToken);
            return result is null
                ? NotFound(new { code = "model.not-found" })
                : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "model.invalid-approval", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "model.not-approvable", message = ex.Message });
        }
    }

    [HttpPost("{id:guid}/pin")]
    public async Task<IActionResult> Pin(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await registry.PinAsync(id, cancellationToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { code = "model.not-found" });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "model.not-pinnable", message = ex.Message });
        }
    }

    [HttpPost("rollback")]
    public async Task<IActionResult> Rollback(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await registry.RollbackAsync(cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "model.rollback-unavailable", message = ex.Message });
        }
    }
}
