using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Services;
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
        [FromQuery] string? status = null)
    {
        var result = await service.GetContractsAsync(page, pageSize, patientId, status);
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
        var result = await service.CreateContractAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateContractRequest req)
    {
        var result = await service.UpdateContractAsync(id, req);
        return result == null ? NotFound(new { message = "العقد غير موجود" }) : Ok(result);
    }
}
