using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    /// <summary>
    /// GET /api/finance-v3/doctor-commissions
    /// Returns aggregated doctor commission statistics.
    /// Access is restricted to Admin and Accountant roles only (via ReportsAccess policy on the class).
    /// </summary>
    [HttpGet("doctor-commissions")]
    public async Task<IActionResult> GetDoctorCommissions(
        [FromQuery] Guid? doctorId,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        // 1. Safe parsing of the date range
        DateOnly fromDate;
        DateOnly toDate;

        if (string.IsNullOrEmpty(from))
        {
            // Default to start of current month
            var today = DateOnly.FromDateTime(DateTime.Today);
            fromDate = new DateOnly(today.Year, today.Month, 1);
        }
        else if (!DateOnly.TryParse(from, out fromDate))
        {
            return BadRequest(new { message = "تاريخ البداية غير صالح" });
        }

        if (string.IsNullOrEmpty(to))
        {
            toDate = DateOnly.FromDateTime(DateTime.Today);
        }
        else if (!DateOnly.TryParse(to, out toDate))
        {
            return BadRequest(new { message = "تاريخ النهاية غير صالح" });
        }

        var startDateTime = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var endDateTime = DateTime.SpecifyKind(toDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        // 2. Query and aggregate invoice items per doctor
        var itemsQuery = db.InvoiceLineItems
            .Include(i => i.Invoice)
            .Where(i => i.IsActive 
                     && i.Invoice.IsActive 
                     && i.Invoice.CreatedAt >= startDateTime 
                     && i.Invoice.CreatedAt <= endDateTime);

        if (doctorId.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.DoctorId == doctorId.Value);
        }

        var items = await itemsQuery
            .Select(i => new 
            { 
                i.DoctorId, 
                i.TotalPrice, 
                i.DoctorCommissionAmount 
            })
            .ToListAsync();

        // 3. Query payments per doctor
        var paymentsQuery = db.DoctorCommissionPayments
            .Where(p => p.IsActive 
                     && p.PaymentDate >= fromDate 
                     && p.PaymentDate <= toDate);

        if (doctorId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.DoctorId == doctorId.Value);
        }

        var payments = await paymentsQuery
            .Select(p => new 
            { 
                p.DoctorId, 
                p.Amount 
            })
            .ToListAsync();

        // 4. Resolve doctor records
        var doctorIds = items
            .Where(i => i.DoctorId.HasValue)
            .Select(i => i.DoctorId!.Value)
            .Concat(payments.Select(p => p.DoctorId))
            .Distinct()
            .ToList();

        if (doctorId.HasValue && !doctorIds.Contains(doctorId.Value))
        {
            doctorIds.Add(doctorId.Value);
        }

        var doctorsMap = await db.Doctors
            .Where(d => doctorIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d);

        // 5. Build DTOs
        var resultList = new List<DoctorCommissionSummaryDto>();
        foreach (var docId in doctorIds)
        {
            var doctor = doctorsMap.GetValueOrDefault(docId);
            if (doctor == null) continue; // Skip if doctor doesn't exist in DB

            var docItems = items.Where(i => i.DoctorId == docId).ToList();
            var docPayments = payments.Where(p => p.DoctorId == docId).ToList();

            var casesCount = docItems.Count;
            var totalServiceValue = docItems.Sum(i => i.TotalPrice);
            var commissionPercentage = doctor.DefaultCommissionPercentage ?? 0m;
            var commissionDue = docItems.Sum(i => i.DoctorCommissionAmount);
            var commissionPaid = docPayments.Sum(p => p.Amount);
            var commissionRemaining = commissionDue - commissionPaid;

            resultList.Add(new DoctorCommissionSummaryDto
            {
                DoctorId = docId,
                DoctorName = doctor.Name,
                CasesCount = casesCount,
                TotalServiceValue = totalServiceValue,
                CommissionPercentage = commissionPercentage,
                CommissionDue = commissionDue,
                CommissionPaid = commissionPaid,
                CommissionRemaining = commissionRemaining
            });
        }

        return Ok(resultList);
    }
}
