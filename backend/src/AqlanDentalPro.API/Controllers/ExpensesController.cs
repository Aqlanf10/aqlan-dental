using AqlanDentalPro.Application.DTOs;
using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/expenses")]
[Authorize(Policy = "FinanceAccess")]
public class ExpensesController(FinanceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? category = null,
        [FromQuery] string? from = null,
        [FromQuery] string? to = null)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : (DateOnly?)null;
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : (DateOnly?)null;
        var result = await service.GetExpensesAsync(page, pageSize, category, fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await service.GetExpenseByIdAsync(id);
        return result == null ? NotFound(new { message = "المصروف غير موجود" }) : Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExpenseRequest req)
    {
        var result = await service.CreateExpenseAsync(req);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateExpenseRequest req)
    {
        var result = await service.UpdateExpenseAsync(id, req);
        return result == null ? NotFound(new { message = "المصروف غير موجود" }) : Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await service.DeleteExpenseAsync(id);
        return deleted ? NoContent() : NotFound(new { message = "المصروف غير موجود" });
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var result = await service.GetExpenseSummaryAsync();
        return Ok(result);
    }
}
