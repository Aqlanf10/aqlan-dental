using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Application.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/contracts")]
[Authorize(Policy = "FinanceAccess")]
public class ContractsController(FinanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? patientId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? specialty = null,
        [FromQuery] string? search = null)
    {
        var result = await service.GetContractsAsync(page, pageSize, patientId, status, specialty, search);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await service.GetContractByIdAsync(id);
        return result == null ? NotFound(new { message = "العقد غير موجود" }) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateContractRequest req)
    {
        var validator = new CreateContractRequestValidator();
        var validationResult = await validator.ValidateAsync(req);
        if (!validationResult.IsValid)
            return BadRequest(new { message = "بيانات غير صالحة", errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        var result = await service.CreateContractAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractRequest req)
    {
        var validator = new UpdateContractRequestValidator();
        var validationResult = await validator.ValidateAsync(req);
        if (!validationResult.IsValid)
            return BadRequest(new { message = "بيانات غير صالحة", errors = validationResult.Errors.Select(e => e.ErrorMessage) });

        var result = await service.UpdateContractAsync(id, req);
        return result == null ? NotFound(new { message = "العقد غير موجود" }) : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await service.DeleteContractAsync(id);
        return deleted ? NoContent() : NotFound(new { message = "العقد غير موجود" });
    }
}
