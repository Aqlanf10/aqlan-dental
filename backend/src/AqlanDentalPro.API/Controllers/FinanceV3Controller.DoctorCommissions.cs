using AqlanDentalPro.Infrastructure.Services;
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
        // Blocker: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        // Verify requested doctor belongs to the branch for non-admins
        if (doctorId.HasValue && branchId.HasValue)
        {
            var doctorExistsInBranch = await db.Doctors.AnyAsync(d => d.Id == doctorId.Value && d.BranchId == branchId.Value);
            if (!doctorExistsInBranch)
            {
                return StatusCode(403, new { message = "ليس لديك صلاحية الوصول إلى بيانات طبيب من فرع آخر" });
            }
        }

        // 1. Safe parsing of the date range
        DateOnly fromDate;
        DateOnly toDate;

        if (string.IsNullOrEmpty(from))
        {
            // Default to start of current month
            var today = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday());
            fromDate = new DateOnly(today.Year, today.Month, 1);
        }
        else if (!DateOnly.TryParse(from, out fromDate))
        {
            return BadRequest(new { message = "تاريخ البداية غير صالح" });
        }

        if (string.IsNullOrEmpty(to))
        {
            toDate = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday());
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

        if (branchId.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.Invoice.Patient.BranchId == branchId.Value);
        }

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

        if (branchId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Doctor.BranchId == branchId.Value);
        }

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

        var doctorsMapQuery = db.Doctors.Where(d => doctorIds.Contains(d.Id));
        if (branchId.HasValue)
        {
            doctorsMapQuery = doctorsMapQuery.Where(d => d.BranchId == branchId.Value);
        }
        var doctorsMap = await doctorsMapQuery.ToDictionaryAsync(d => d.Id, d => d);

        // 5. Build DTOs
        var resultList = new List<DoctorCommissionSummaryDto>();
        foreach (var docId in doctorIds)
        {
            var doctor = doctorsMap.GetValueOrDefault(docId);
            if (doctor == null) continue; // Skip if doctor doesn't exist in DB or branch

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

    /// <summary>
    /// GET /api/finance-v3/doctor-commissions/earned-from-collections
    /// Returns commission calculated based on ACTUAL payment collections, not just invoice amounts.
    /// Commission = (Collected Amount - Lab Cost - Material Cost - Other Direct Costs) * Doctor Percentage
    /// Only payments that have been actually collected are counted.
    /// </summary>
    [HttpGet("doctor-commissions/earned-from-collections")]
    public async Task<IActionResult> GetDoctorCommissionsEarnedFromCollections(
        [FromQuery] Guid? doctorId,
        [FromQuery] string? from,
        [FromQuery] string? to)
    {
        // Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        // Verify requested doctor belongs to the branch for non-admins
        if (doctorId.HasValue && branchId.HasValue)
        {
            var doctorExistsInBranch = await db.Doctors.AnyAsync(d => d.Id == doctorId.Value && d.BranchId == branchId.Value);
            if (!doctorExistsInBranch)
            {
                return StatusCode(403, new { message = "ليس لديك صلاحية الوصول إلى بيانات طبيب من فرع آخر" });
            }
        }

        // 1. Safe parsing of the date range
        DateOnly fromDate;
        DateOnly toDate;

        if (string.IsNullOrEmpty(from))
        {
            var today = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday());
            fromDate = new DateOnly(today.Year, today.Month, 1);
        }
        else if (!DateOnly.TryParse(from, out fromDate))
        {
            return BadRequest(new { message = "تاريخ البداية غير صالح" });
        }

        if (string.IsNullOrEmpty(to))
        {
            toDate = DateOnly.FromDateTime(ClinicTimeProvider.ClinicToday());
        }
        else if (!DateOnly.TryParse(to, out toDate))
        {
            return BadRequest(new { message = "تاريخ النهاية غير صالح" });
        }

        var startDateTime = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var endDateTime = DateTime.SpecifyKind(toDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        // 2. Get all invoices with their payments and line items (FIN-10: filter by PAYMENT date, not invoice creation date).
        // Previously filtered by i.CreatedAt, which attributed collected revenue to the invoice-creation period
        // instead of the payment-collection period. A payment collected in July for an invoice created in June
        // appeared in the June report. Now we load invoices whose payments fall in the date range.
        var invoicesQuery = db.Invoices
            .Include(i => i.Patient)
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Where(i => i.IsActive
                     && i.Payments.Any(p => p.IsActive
                                          && p.PaymentDate >= fromDate
                                          && p.PaymentDate <= toDate));

        if (branchId.HasValue)
            invoicesQuery = invoicesQuery.Where(i => i.Patient.BranchId == branchId.Value);
        if (doctorId.HasValue)
            invoicesQuery = invoicesQuery.Where(i => i.LineItems.Any(l => l.DoctorId == doctorId.Value));

        var invoices = await invoicesQuery.ToListAsync();

        // Explicitly filter inactive line items and payments in-memory
        // (ensures correct behavior across all database providers including InMemory)
        // FIN-10: Also filter payments to only those within the date range (the invoice query
        // loads all payments for matching invoices, but we should only aggregate the in-range ones).
        foreach (var inv in invoices)
        {
            inv.LineItems = inv.LineItems.Where(l => l.IsActive).ToList();
            inv.Payments = inv.Payments.Where(p => p.IsActive
                                               && p.PaymentDate >= fromDate
                                               && p.PaymentDate <= toDate).ToList();
        }

        // 3. Get doctor commission payments
        var commissionPaymentsQuery = db.DoctorCommissionPayments
            .Where(p => p.IsActive && p.PaymentDate >= fromDate && p.PaymentDate <= toDate);
        if (branchId.HasValue)
            commissionPaymentsQuery = commissionPaymentsQuery.Where(p => p.Doctor.BranchId == branchId.Value);
        if (doctorId.HasValue)
            commissionPaymentsQuery = commissionPaymentsQuery.Where(p => p.DoctorId == doctorId.Value);

        var commissionPayments = await commissionPaymentsQuery.ToListAsync();

        // 4. Group by doctor
        var doctorIds = invoices
            .SelectMany(i => i.LineItems.Where(l => l.DoctorId.HasValue).Select(l => l.DoctorId!.Value))
            .Distinct()
            .ToList();
        if (doctorId.HasValue && !doctorIds.Contains(doctorId.Value))
            doctorIds.Add(doctorId.Value);

        var doctorsMapQuery = db.Doctors.Where(d => doctorIds.Contains(d.Id));
        if (branchId.HasValue)
            doctorsMapQuery = doctorsMapQuery.Where(d => d.BranchId == branchId.Value);
        var doctorsMap = await doctorsMapQuery.ToDictionaryAsync(d => d.Id, d => d);

        // 5. Build results per doctor
        var result = new List<DoctorCommissionEarnedFromCollectionsDto>();
        foreach (var docId in doctorIds)
        {
            var doctor = doctorsMap.GetValueOrDefault(docId);
            if (doctor == null) continue;

            // For this doctor, collect all line items from invoices
            var docLineItems = invoices
                .SelectMany(i => i.LineItems.Where(l => l.DoctorId == docId))
                .ToList();

            // For each invoice containing this doctor's items, calculate collection ratio
            decimal totalCollected = 0;
            decimal totalLabCost = 0;
            decimal totalMaterialCost = 0;
            decimal totalOtherDirectCosts = 0;
            decimal totalServiceValue = 0;
            decimal totalEarnedCommission = 0;
            int casesCount = 0;

            foreach (var invoice in invoices.Where(inv => inv.LineItems.Any(l => l.DoctorId == docId)))
            {
                var invoiceTotal = invoice.TotalAmount;
                var invoicePaid = invoice.Payments.Sum(p => p.Amount);
                var collectionRatio = invoiceTotal > 0 ? Math.Min(1m, invoicePaid / invoiceTotal) : 0m;

                var docItems = invoice.LineItems.Where(l => l.DoctorId == docId).ToList();
                foreach (var item in docItems)
                {
                    casesCount++;
                    var itemServiceValue = item.TotalPrice;
                    var proportionalCollected = itemServiceValue * collectionRatio;
                    var proportionalLabCost = item.LabCost * collectionRatio;
                    var proportionalMaterialCost = item.MaterialCost * collectionRatio;
                    var proportionalOtherCosts = item.OtherDirectCost * collectionRatio;

                    totalServiceValue += itemServiceValue;
                    totalCollected += proportionalCollected;
                    totalLabCost += proportionalLabCost;
                    totalMaterialCost += proportionalMaterialCost;
                    totalOtherDirectCosts += proportionalOtherCosts;

                    // Net commissionable = collected - costs
                    var netCommissionable = Math.Max(0, proportionalCollected - proportionalLabCost - proportionalMaterialCost - proportionalOtherCosts);
                    var earnedCommission = netCommissionable * (item.DoctorCommissionPercentage / 100m);
                    totalEarnedCommission += earnedCommission;
                }
            }

            var commissionPaid = commissionPayments.Where(p => p.DoctorId == docId).Sum(p => p.Amount);
            var commissionRemaining = Math.Max(0, totalEarnedCommission - commissionPaid);
            var netCommissionableAmount = Math.Max(0, totalCollected - totalLabCost - totalMaterialCost - totalOtherDirectCosts);

            result.Add(new DoctorCommissionEarnedFromCollectionsDto
            {
                DoctorId = docId,
                DoctorName = doctor.Name,
                CasesCount = casesCount,
                TotalServiceValue = totalServiceValue,
                TotalCollected = totalCollected,
                TotalLabCost = totalLabCost,
                TotalMaterialCost = totalMaterialCost,
                TotalOtherDirectCosts = totalOtherDirectCosts,
                NetCommissionableAmount = netCommissionableAmount,
                DoctorPercentage = doctor.DefaultCommissionPercentage ?? 0m,
                CommissionDue = totalEarnedCommission,
                CommissionPaid = commissionPaid,
                CommissionRemaining = commissionRemaining
            });
        }

        return Ok(result);
    }
}
