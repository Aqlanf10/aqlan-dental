using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// SEQ-06: Temporary diagnostic endpoint to capture the actual exception
/// messages from the 3 live 500 errors. Admin-only. Returns exception type +
/// message + stack trace top frame so we can root-cause without Railway logs.
/// Remove after diagnosis is complete.
/// </summary>
[ApiController]
[Route("api/diagnostic")]
[Authorize(Policy = "AdminOnly")]
public class DiagnosticController(AppDbContext db, IContractService contractService) : ControllerBase
{
    [HttpGet("seq-06-contracts")]
    public async Task<IActionResult> DiagnoseContracts()
    {
        try
        {
            var result = await contractService.GetContractsAsync(1, 1, null, "active");
            return Ok(new { ok = true, count = result.Count });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                ok = false,
                errorType = ex.GetType().FullName,
                message = ex.Message,
                innerType = ex.InnerException?.GetType().FullName,
                innerMessage = ex.InnerException?.Message,
                stackTop = ex.StackTrace?.Split('\n').Take(5).ToArray()
            });
        }
    }

    [HttpGet("seq-06-expenses")]
    public async Task<IActionResult> DiagnoseExpenses()
    {
        try
        {
            var query = db.OperationalExpenses
                .Include(e => e.Supplier)
                .Where(e => e.IsActive);

            var total = await query.CountAsync();

            var page = await query
                .OrderByDescending(e => e.ExpenseDate)
                .Take(1)
                .Select(e => new
                {
                    e.Id,
                    e.Title,
                    e.Category,
                    e.Amount,
                    e.PaymentMethod,
                    ExpenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                    e.ApprovalStatus,
                    RequestedBy = e.PaidBy.ToString(),
                })
                .ToListAsync();

            return Ok(new { ok = true, total, pageCount = page.Count });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                ok = false,
                errorType = ex.GetType().FullName,
                message = ex.Message,
                innerType = ex.InnerException?.GetType().FullName,
                innerMessage = ex.InnerException?.Message,
                stackTop = ex.StackTrace?.Split('\n').Take(5).ToArray()
            });
        }
    }

    [HttpGet("seq-06-dashboard")]
    public async Task<IActionResult> DiagnoseDashboard([FromServices] DashboardService service)
    {
        try
        {
            var stats = await service.GetStatsAsync(true);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                ok = false,
                errorType = ex.GetType().FullName,
                message = ex.Message,
                innerType = ex.InnerException?.GetType().FullName,
                innerMessage = ex.InnerException?.Message,
                stackTop = ex.StackTrace?.Split('\n').Take(5).ToArray()
            });
        }
    }
}
