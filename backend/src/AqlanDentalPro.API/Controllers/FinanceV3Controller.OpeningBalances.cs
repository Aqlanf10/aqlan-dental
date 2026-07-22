using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    public sealed class CreatePatientOpeningReceivableRequest
    {
        public Guid PatientId { get; init; }
        public decimal Amount { get; init; }
        public string Currency { get; init; } = "YER";
        public decimal? ExchangeRateToYer { get; init; }
        public DateOnly BalanceDate { get; init; }
        public string? Reference { get; init; }
        public string? Notes { get; init; }
    }

    [HttpGet("opening-balances")]
    public async Task<IActionResult> GetOpeningBalances()
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId == Guid.Empty))
            return StatusCode(403, new { message = "ليس لديك فرع معين. تواصل مع الإدارة." });

        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var patientBalances = await db.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.IsActive
                && invoice.IsOpeningBalance
                && (!branchId.HasValue || invoice.Patient.BranchId == branchId.Value))
            .Select(invoice => new
            {
                invoice.Id,
                CounterpartyId = invoice.PatientId,
                CounterpartyName = (invoice.Patient.FirstName + " " + invoice.Patient.LastName).Trim(),
                Kind = "PatientReceivable",
                Reference = invoice.InvoiceNumber,
                Amount = invoice.TotalAmount,
                PaidAmount = (invoice.Payments.Where(payment => payment.IsActive && payment.Amount > 0)
                    .Sum(payment => (decimal?)(payment.AppliedAmount == 0 ? payment.Amount : payment.AppliedAmount)) ?? 0m)
                    + (invoice.PaymentAllocations.Where(allocation => allocation.IsActive)
                        .Sum(allocation => (decimal?)allocation.Amount) ?? 0m),
                Currency = string.IsNullOrWhiteSpace(invoice.Currency) ? "YER" : invoice.Currency,
                invoice.Status,
                invoice.Notes,
                invoice.CreatedAt
            })
            .ToListAsync();

        var patientInvoiceIds = patientBalances.Select(item => item.Id).ToList();
        var patientJournalData = await db.JournalEntries
            .AsNoTracking()
            .Where(entry => patientInvoiceIds.Contains(entry.FinancialDocumentId)
                && entry.FinancialDocumentType == FinancialDocumentType.OpeningBalance
                && !entry.IsReversal)
            .Select(entry => new
            {
                InvoiceId = entry.FinancialDocumentId,
                entry.EntryDate,
                entry.ExchangeRateToYer,
                entry.ReversedByEntryId
            })
            .ToDictionaryAsync(item => item.InvoiceId);

        var supplierBalances = await db.SupplierBills
            .AsNoTracking()
            .Where(bill => bill.IsActive
                && bill.IsOpeningBalance
                && (!branchId.HasValue || bill.BranchId == branchId.Value))
            .Select(bill => new
            {
                bill.Id,
                CounterpartyId = bill.SupplierId,
                CounterpartyName = bill.Supplier != null ? bill.Supplier.Name : "",
                Kind = "SupplierPayable",
                Reference = bill.BillNumber,
                Amount = bill.TotalAmount,
                PaidAmount = bill.PaidAmount,
                Currency = string.IsNullOrWhiteSpace(bill.Currency) ? "YER" : bill.Currency,
                ExchangeRateToYer = bill.ExchangeRateToYer,
                EntryDate = bill.BillDate,
                Status = bill.Status.ToString(),
                bill.Notes,
                bill.CreatedAt
            })
            .ToListAsync();

        var items = patientBalances.Select(item =>
        {
            patientJournalData.TryGetValue(item.Id, out var journal);
            return new
            {
                item.Id,
                item.CounterpartyId,
                item.CounterpartyName,
                item.Kind,
                item.Reference,
                item.Amount,
                item.PaidAmount,
                OutstandingAmount = Math.Max(0m, item.Amount - item.PaidAmount),
                item.Currency,
                ExchangeRateToYer = journal?.ExchangeRateToYer ?? 1m,
                EntryDate = journal?.EntryDate ?? DateOnly.FromDateTime(item.CreatedAt),
                Status = item.Status.ToString(),
                IsReversed = journal?.ReversedByEntryId.HasValue ?? false,
                item.Notes,
                item.CreatedAt
            };
        }).Concat(supplierBalances.Select(item => new
        {
            item.Id,
            item.CounterpartyId,
            item.CounterpartyName,
            item.Kind,
            item.Reference,
            item.Amount,
            item.PaidAmount,
            OutstandingAmount = Math.Max(0m, item.Amount - item.PaidAmount),
            item.Currency,
            item.ExchangeRateToYer,
            item.EntryDate,
            item.Status,
            IsReversed = false,
            item.Notes,
            item.CreatedAt
        }))
        .OrderByDescending(item => item.EntryDate)
        .ThenByDescending(item => item.CreatedAt)
        .ToList();

        return Ok(new
        {
            data = items,
            totalsByCurrency = items
                .Where(item => !item.IsReversed)
                .GroupBy(item => new { item.Kind, item.Currency })
                .Select(group => new
                {
                    group.Key.Kind,
                    group.Key.Currency,
                    Amount = group.Sum(item => item.Amount),
                    PaidAmount = group.Sum(item => item.PaidAmount),
                    OutstandingAmount = group.Sum(item => item.OutstandingAmount)
                })
                .OrderBy(item => item.Kind)
                .ThenBy(item => item.Currency)
        });
    }

    [HttpPost("opening-balances/patient-receivables")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreatePatientOpeningReceivable(
        [FromBody] CreatePatientOpeningReceivableRequest req)
    {
        if (!await CanAsync("finance.invoices", "create")) return Deny();

        var branchId = await ResolveBranchIdAsync();
        var userId = currentUser.UserId ?? Guid.Empty;
        if (branchId == Guid.Empty || userId == Guid.Empty)
            return BadRequest(new { message = "تعذر تحديد الفرع أو المستخدم." });
        if (req.Amount <= 0)
            return BadRequest(new { message = "مبلغ الرصيد السابق يجب أن يكون أكبر من صفر." });
        if (req.BalanceDate == default)
            return BadRequest(new { message = "تاريخ الرصيد السابق مطلوب." });

        var currency = req.Currency.Trim().ToUpperInvariant();
        if (currency is not ("YER" or "SAR" or "USD"))
            return BadRequest(new { message = "العملة يجب أن تكون YER أو SAR أو USD." });
        var exchangeRateToYer = currency == "YER" ? 1m : req.ExchangeRateToYer.GetValueOrDefault();
        if (exchangeRateToYer <= 0)
            return BadRequest(new { message = "سعر الصرف الفعلي إلى الريال اليمني مطلوب للعملات الأجنبية." });

        var patient = await db.Patients.FirstOrDefaultAsync(item =>
            item.Id == req.PatientId
            && item.IsActive
            && item.BranchId == branchId);
        if (patient is null)
            return BadRequest(new { message = "المريض غير موجود في الفرع المحدد." });

        var dateIsClosed = await db.AccountingPeriods.AnyAsync(period =>
            period.BranchId == branchId
            && period.IsActive
            && period.Status == "Closed"
            && req.BalanceDate >= period.StartDate
            && req.BalanceDate <= period.EndDate);
        if (dateIsClosed)
            return Conflict(new { message = "لا يمكن إدخال رصيد بتاريخ يقع داخل فترة مالية مقفلة." });

        var reference = req.Reference?.Trim();
        if (reference?.Length > 100)
            return BadRequest(new { message = "مرجع الرصيد لا يتجاوز 100 حرف." });
        var notes = req.Notes?.Trim();
        if (notes?.Length > 1000)
            return BadRequest(new { message = "الملاحظات لا تتجاوز 1000 حرف." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            if (db.Database.IsRelational())
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock({0})",
                    StableLockKeyHelper.InvoiceNumber);

            var prefix = $"OB-PAT-{req.BalanceDate:yyyyMMdd}-";
            var lastNumber = await db.Invoices
                .IgnoreQueryFilters()
                .Where(invoice => invoice.InvoiceNumber.StartsWith(prefix))
                .OrderByDescending(invoice => invoice.InvoiceNumber)
                .Select(invoice => invoice.InvoiceNumber)
                .FirstOrDefaultAsync();
            var sequence = 1;
            if (!string.IsNullOrWhiteSpace(lastNumber)
                && int.TryParse(lastNumber[prefix.Length..], out var previousSequence))
                sequence = previousSequence + 1;
            var invoiceNumber = $"{prefix}{sequence:D3}";

            var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
            var invoice = new Invoice
            {
                PatientId = patient.Id,
                InvoiceNumber = invoiceNumber,
                Currency = currency,
                Status = InvoiceStatus.Issued,
                Subtotal = req.Amount,
                TotalAmount = req.Amount,
                IsOpeningBalance = true,
                Notes = string.Join(" | ", new[]
                    {
                        "رصيد سابق قبل استخدام النظام",
                        string.IsNullOrWhiteSpace(reference) ? null : $"المرجع: {reference}",
                        notes
                    }.Where(value => !string.IsNullOrWhiteSpace(value))),
                CreatedBy = userId,
                UpdatedBy = userId,
                CreatedAt = req.BalanceDate.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc)
            };
            invoice.LineItems.Add(new InvoiceLineItem
            {
                ServiceNameSnapshot = "رصيد سابق",
                Description = string.IsNullOrWhiteSpace(reference)
                    ? "مديونية سابقة قبل استخدام النظام"
                    : $"مديونية سابقة - {reference}",
                Quantity = 1,
                UnitPrice = req.Amount,
                TotalPrice = req.Amount
            });
            db.Invoices.Add(invoice);

            var journalEntry = await journalEntryService.CreateEntryAsync(
                FinancialDocumentType.OpeningBalance,
                invoice.Id,
                $"رصيد افتتاحي مستحق على المريض {patientName}",
                req.BalanceDate,
                branchId,
                userId,
                cashierSessionId: null,
                treasuryId: null,
                lines:
                [
                    (JournalAccountType.PatientReceivable, patient.Id, req.Amount, 0m,
                        $"رصيد سابق مستحق على {patientName}"),
                    (JournalAccountType.OwnerEquity, branchId, 0m, req.Amount,
                        "موازنة الرصيد الافتتاحي ضمن حقوق الملكية")
                ],
                autoSave: false);
            journalEntry.Currency = currency;
            journalEntry.ExchangeRateToYer = exchangeRateToYer;
            journalEntry.IsPosted = true;
            journalEntry.PostedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            await audit.LogAsync(AuditAction.Create, "PatientOpeningReceivable", invoice.Id,
                details: $"patient={patient.Id}; amount={req.Amount}; currency={currency}; rate={exchangeRateToYer}; journal={journalEntry.Id}");

            return Ok(new
            {
                invoice.Id,
                invoice.InvoiceNumber,
                PatientId = patient.Id,
                PatientName = patientName,
                invoice.TotalAmount,
                invoice.Currency,
                ExchangeRateToYer = exchangeRateToYer,
                BalanceDate = req.BalanceDate,
                JournalEntryId = journalEntry.Id,
                message = "تم تسجيل مديونية المريض السابقة وترحيل قيدها، ويمكن تحصيلها من شاشة المقبوضات."
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
