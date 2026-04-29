using AqlanDentalPro.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/finance")]
[Authorize(Policy = "FinanceAccess")]
public class FinanceReportsController(FinanceService service) : ControllerBase
{
    [HttpGet("by-specialty")]
    public async Task<IActionResult> GetBySpecialty(
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        var result = await service.GetFinanceBySpecialtyAsync(fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("by-doctor")]
    public async Task<IActionResult> GetByDoctor(
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        var result = await service.GetFinanceByDoctorAsync(fromDate, toDate);
        return Ok(result);
    }

    [HttpGet("daily-report")]
    public async Task<IActionResult> GetDailyReport([FromQuery] string? date)
    {
        var reportDate = !string.IsNullOrWhiteSpace(date) ? DateOnly.Parse(date) : DateOnly.FromDateTime(DateTime.Today);

        var result = await service.GetDailyReportAsync(reportDate);
        return Ok(result);
    }

    [HttpGet("monthly-report")]
    public async Task<IActionResult> GetMonthlyReport(
        [FromQuery] int? year,
        [FromQuery] int? month)
    {
        var now = DateTime.Today;
        var reportYear = year ?? now.Year;
        var reportMonth = month ?? now.Month;

        if (reportMonth < 1 || reportMonth > 12)
            return BadRequest(new { message = "الشهر يجب أن يكون بين 1 و12" });

        var result = await service.GetMonthlyReportAsync(reportYear, reportMonth);
        return Ok(result);
    }

    [HttpGet("profit-loss")]
    public async Task<IActionResult> GetProfitLoss(
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        var fromDate = !string.IsNullOrWhiteSpace(from) ? DateOnly.Parse(from) : DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var toDate = !string.IsNullOrWhiteSpace(to) ? DateOnly.Parse(to) : DateOnly.FromDateTime(DateTime.Today);

        var revenue = await service.GetFinanceBySpecialtyAsync(fromDate, toDate);
        var expenses = await service.GetExpenseSummaryAsync();
        var totalRevenue = revenue.Sum(r => r.TotalRevenue);
        var totalExpenses = expenses.MonthExpenses; // approximate for date range
        var netProfit = totalRevenue - totalExpenses;

        return Ok(new
        {
            fromDate = fromDate.ToString("yyyy-MM-dd"),
            toDate = toDate.ToString("yyyy-MM-dd"),
            totalRevenue,
            totalExpenses,
            netProfit,
            profitMargin = totalRevenue > 0 ? Math.Round((netProfit / totalRevenue) * 100, 1) : 0,
            revenue = revenue.Select(r => new { r.Specialty, r.SpecialtyAr, r.TotalRevenue }).ToList(),
            expenses = expenses.ByCategory
        });
    }
}
