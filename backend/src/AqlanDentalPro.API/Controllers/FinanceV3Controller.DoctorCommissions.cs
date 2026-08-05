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
        [FromQuery] string? to,
        [FromQuery(Name = "branchId")] Guid? requestedBranchId = null)
    {
        if (!await CanAsync("finance.commissions", "view")) return Deny();
        // Blocker: Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? requestedBranchId : currentUser.BranchId;

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
            var today = ClinicTimeProvider.ClinicToday();
            fromDate = new DateOnly(today.Year, today.Month, 1);
        }
        else if (!DateOnly.TryParse(from, out fromDate))
        {
            return BadRequest(new { message = "تاريخ البداية غير صالح" });
        }

        if (string.IsNullOrEmpty(to))
        {
            toDate = ClinicTimeProvider.ClinicToday();
        }
        else if (!DateOnly.TryParse(to, out toDate))
        {
            return BadRequest(new { message = "تاريخ النهاية غير صالح" });
        }

        var startDateTime = DateTime.SpecifyKind(fromDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var endDateTime = DateTime.SpecifyKind(toDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

        // 2. Aggregate invoice items per doctor (server-side GROUP BY ... SUM).
        // FIN-13: previously loaded all matching InvoiceLineItems into memory and then
        // ran in-memory `.Where(i => i.DoctorId == docId).Sum(i => i.TotalPrice)` per
        // doctor (an O(doctors × items) scan). Now a single SQL GROUP BY returns one
        // row per doctor with all three aggregates already computed.
        var itemsQuery = db.InvoiceLineItems
            .Where(i => i.IsActive
                     && i.Invoice.IsActive
                     && i.Invoice.Status != InvoiceStatus.Cancelled
                     && i.Invoice.CreatedAt >= startDateTime
                     && i.Invoice.CreatedAt <= endDateTime);

        if (branchId.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.Invoice.Patient.BranchId == branchId.Value);
        }

        var itemsByDoctor = await itemsQuery
            .Where(i => i.DoctorId.HasValue)
            .GroupBy(i => new
            {
                DoctorId = i.DoctorId!.Value,
                BranchId = i.Invoice.Patient.BranchId ?? Guid.Empty,
                Currency = i.Invoice.Currency
            })
            .Select(g => new
            {
                g.Key.DoctorId,
                g.Key.BranchId,
                g.Key.Currency,
                CasesCount = g.Count(),
                TotalServiceValue = g.Sum(i => i.TotalPrice),
                CommissionDue = g.Sum(i => i.DoctorCommissionAmount),
                CommissionPercentageTotal = g.Sum(i => i.DoctorCommissionPercentage)
            })
            .ToDictionaryAsync(
                x => (x.DoctorId, x.BranchId, x.Currency),
                x => new DoctorCommissionAggregate(
                    x.CasesCount,
                    x.TotalServiceValue,
                    x.CommissionDue,
                    x.CommissionPercentageTotal));

        // Older visit-generated invoices can have a calculated commission but no
        // line-item DoctorId. Resolve those rows through the visit/appointment so
        // they remain visible while the one-time data repair is being deployed.
        var orphanItems = await itemsQuery
            .Where(i => !i.DoctorId.HasValue)
            .Select(i => new
            {
                i.RelatedVisitId,
                InvoiceVisitId = i.Invoice.VisitId,
                InvoiceAppointmentId = i.Invoice.AppointmentId,
                i.TotalPrice,
                i.DoctorCommissionAmount,
                i.DoctorCommissionPercentage,
                BranchId = i.Invoice.Patient.BranchId,
                i.Invoice.Currency
            })
            .ToListAsync();

        if (orphanItems.Count > 0)
        {
            var visitIds = orphanItems
                .SelectMany(i => new[] { i.RelatedVisitId, i.InvoiceVisitId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var visits = await db.Visits
                .Where(v => visitIds.Contains(v.Id))
                .Select(v => new { v.Id, v.DoctorId, v.AppointmentId })
                .ToDictionaryAsync(v => v.Id);

            var appointmentIds = orphanItems
                .Select(i => i.InvoiceAppointmentId)
                .Concat(visits.Values.Select(v => v.AppointmentId))
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var appointmentDoctors = await db.Appointments
                .Where(a => appointmentIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, a => a.DoctorId);

            foreach (var item in orphanItems)
            {
                var visit = item.RelatedVisitId.HasValue
                    ? visits.GetValueOrDefault(item.RelatedVisitId.Value)
                    : null;
                visit ??= item.InvoiceVisitId.HasValue
                    ? visits.GetValueOrDefault(item.InvoiceVisitId.Value)
                    : null;

                var effectiveDoctorId = visit?.DoctorId;
                if (!effectiveDoctorId.HasValue && visit?.AppointmentId.HasValue == true)
                    effectiveDoctorId = appointmentDoctors.TryGetValue(visit.AppointmentId.Value, out var visitDoctorId)
                        ? visitDoctorId
                        : null;
                if (!effectiveDoctorId.HasValue && item.InvoiceAppointmentId.HasValue)
                    effectiveDoctorId = appointmentDoctors.TryGetValue(item.InvoiceAppointmentId.Value, out var invoiceDoctorId)
                        ? invoiceDoctorId
                        : null;

                if (!effectiveDoctorId.HasValue
                    || (doctorId.HasValue && effectiveDoctorId.Value != doctorId.Value))
                    continue;

                var resolvedBranchId = item.BranchId
                    ?? await db.Doctors.Where(doctor => doctor.Id == effectiveDoctorId.Value)
                        .Select(doctor => doctor.BranchId)
                        .FirstOrDefaultAsync()
                    ?? Guid.Empty;
                var scope = (effectiveDoctorId.Value, resolvedBranchId, item.Currency);
                var existing = itemsByDoctor.GetValueOrDefault(scope)
                    ?? new DoctorCommissionAggregate(0, 0m, 0m, 0m);
                itemsByDoctor[scope] = existing with
                {
                    CasesCount = existing.CasesCount + 1,
                    TotalServiceValue = existing.TotalServiceValue + item.TotalPrice,
                    CommissionDue = existing.CommissionDue + item.DoctorCommissionAmount,
                    CommissionPercentageTotal = existing.CommissionPercentageTotal + item.DoctorCommissionPercentage
                };
            }
        }

        if (doctorId.HasValue)
        {
            itemsByDoctor = itemsByDoctor
                .Where(pair => pair.Key.Item1 == doctorId.Value)
                .ToDictionary(pair => pair.Key, pair => pair.Value);
        }

        // 3. Aggregate commission payments per doctor (server-side GROUP BY ... SUM)
        var paymentsQuery = db.DoctorCommissionPayments
            .Where(p => p.IsActive
                     && p.PaymentDate >= fromDate
                     && p.PaymentDate <= toDate);

        if (branchId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.BranchId == branchId.Value);
        }

        if (doctorId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.DoctorId == doctorId.Value);
        }

        var paymentsByDoctor = await paymentsQuery
            .GroupBy(p => new { p.DoctorId, p.BranchId, p.Currency })
            .Select(g => new
            {
                g.Key.DoctorId,
                g.Key.BranchId,
                g.Key.Currency,
                CommissionPaid = g.Sum(p => p.Amount)
            })
            .ToDictionaryAsync(x => (x.DoctorId, x.BranchId, x.Currency));

        // 4. Resolve doctor records
        var scopes = itemsByDoctor.Keys
            .Concat(paymentsByDoctor.Keys)
            .Distinct()
            .ToList();

        var doctorIds = scopes.Select(scope => scope.Item1).Distinct().ToList();

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

        // 5. Build DTOs from the pre-aggregated per-doctor rows
        var resultList = new List<DoctorCommissionSummaryDto>();
        foreach (var scope in scopes)
        {
            var docId = scope.Item1;
            var doctor = doctorsMap.GetValueOrDefault(docId);
            if (doctor == null) continue; // Skip if doctor doesn't exist in DB or branch

            itemsByDoctor.TryGetValue(scope, out var items);
            paymentsByDoctor.TryGetValue(scope, out var payments);

            var casesCount = items?.CasesCount ?? 0;
            var totalServiceValue = items?.TotalServiceValue ?? 0m;
            var commissionPercentage = casesCount > 0 && items!.CommissionPercentageTotal > 0
                ? items!.CommissionPercentageTotal / casesCount
                : doctor.DefaultCommissionPercentage ?? 0m;
            var commissionDue = items?.CommissionDue ?? 0m;
            var commissionPaid = payments?.CommissionPaid ?? 0m;
            var commissionRemaining = commissionDue - commissionPaid;

            resultList.Add(new DoctorCommissionSummaryDto
            {
                DoctorId = docId,
                BranchId = scope.Item2,
                Currency = scope.Item3,
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

    private sealed record DoctorCommissionAggregate(
        int CasesCount,
        decimal TotalServiceValue,
        decimal CommissionDue,
        decimal CommissionPercentageTotal);

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
        [FromQuery] string? to,
        [FromQuery(Name = "branchId")] Guid? requestedBranchId = null)
    {
        if (!await CanAsync("finance.commissions", "view")) return Deny();
        // Branch isolation guard for non-admin users
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? requestedBranchId : currentUser.BranchId;

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
            var today = ClinicTimeProvider.ClinicToday();
            fromDate = new DateOnly(today.Year, today.Month, 1);
        }
        else if (!DateOnly.TryParse(from, out fromDate))
        {
            return BadRequest(new { message = "تاريخ البداية غير صالح" });
        }

        if (string.IsNullOrEmpty(to))
        {
            toDate = ClinicTimeProvider.ClinicToday();
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
                     && (i.Payments.Any(p => p.IsActive
                                           && p.PaymentDate >= fromDate
                                           && p.PaymentDate <= toDate)
                      || (!i.Payments.Any(p => p.IsActive)
                          && i.CreatedAt >= startDateTime
                          && i.CreatedAt <= endDateTime)));

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
            commissionPaymentsQuery = commissionPaymentsQuery.Where(p => p.BranchId == branchId.Value);
        if (doctorId.HasValue)
            commissionPaymentsQuery = commissionPaymentsQuery.Where(p => p.DoctorId == doctorId.Value);

        var commissionPayments = await commissionPaymentsQuery.ToListAsync();

        // 4. Group by doctor
        var scopes = invoices
            .SelectMany(invoice => invoice.LineItems
                .Where(line => line.DoctorId.HasValue)
                .Select(line => (
                    DoctorId: line.DoctorId!.Value,
                    BranchId: invoice.Patient.BranchId ?? Guid.Empty,
                    Currency: invoice.Currency)))
            .Distinct()
            .ToList();

        var doctorIds = scopes.Select(scope => scope.DoctorId).Distinct().ToList();

        var doctorsMapQuery = db.Doctors.Where(d => doctorIds.Contains(d.Id));
        if (branchId.HasValue)
            doctorsMapQuery = doctorsMapQuery.Where(d => d.BranchId == branchId.Value);
        var doctorsMap = await doctorsMapQuery.ToDictionaryAsync(d => d.Id, d => d);

        // 5. Build results per doctor
        var result = new List<DoctorCommissionEarnedFromCollectionsDto>();
        foreach (var scope in scopes)
        {
            var docId = scope.DoctorId;
            var doctor = doctorsMap.GetValueOrDefault(docId);
            if (doctor == null) continue;

            // For this doctor, collect all line items from invoices
            var docLineItems = invoices
                .Where(invoice => (invoice.Patient.BranchId ?? Guid.Empty) == scope.BranchId
                               && invoice.Currency == scope.Currency)
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

            foreach (var invoice in invoices.Where(inv => (inv.Patient.BranchId ?? Guid.Empty) == scope.BranchId
                                                        && inv.Currency == scope.Currency
                                                        && inv.LineItems.Any(l => l.DoctorId == docId)))
            {
                var invoiceTotal = invoice.TotalAmount;
                var invoicePaid = invoice.Payments
                    .Where(payment => payment.AccountCurrency == scope.Currency)
                    .Sum(payment => payment.AppliedAmount > 0 ? payment.AppliedAmount : payment.Amount);
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

            var commissionPaid = commissionPayments
                .Where(payment => payment.DoctorId == docId
                               && payment.BranchId == scope.BranchId
                               && payment.Currency == scope.Currency)
                .Sum(payment => payment.Amount);
            var commissionRemaining = Math.Max(0, totalEarnedCommission - commissionPaid);
            var netCommissionableAmount = Math.Max(0, totalCollected - totalLabCost - totalMaterialCost - totalOtherDirectCosts);

            result.Add(new DoctorCommissionEarnedFromCollectionsDto
            {
                DoctorId = docId,
                BranchId = scope.BranchId,
                Currency = scope.Currency,
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
