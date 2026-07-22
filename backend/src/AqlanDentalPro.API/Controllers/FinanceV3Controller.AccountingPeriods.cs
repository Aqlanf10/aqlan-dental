using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    public sealed record CreateAccountingPeriodRequest(string Name, DateOnly StartDate, DateOnly EndDate, string? Notes);
    public sealed record YearEndCloseRequest(string? NextPeriodName);

    private static readonly JournalAccountType[] ProfitAndLossAccountTypes =
    [
        JournalAccountType.Revenue,
        JournalAccountType.Expense,
        JournalAccountType.ContraRevenue,
        JournalAccountType.ContraExpense,
        JournalAccountType.SalesReturns
    ];

    private static bool IsAnnualPeriod(AccountingPeriod period) =>
        period.EndDate == period.StartDate.AddYears(1).AddDays(-1);

    [HttpGet("accounting-periods")]
    public async Task<IActionResult> GetAccountingPeriods()
    {
        if (!await CanAsync("finance.reports", "view")) return Deny();
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;
        var periods = await db.AccountingPeriods
            .Where(period => !branchId.HasValue || period.BranchId == branchId.Value)
            .OrderByDescending(period => period.EndDate)
            .ToListAsync();
        var periodIds = periods.Select(period => period.Id).ToList();
        var closingEntryCounts = await db.JournalEntries
            .Where(entry => periodIds.Contains(entry.FinancialDocumentId)
                && entry.FinancialDocumentType == FinancialDocumentType.YearEndClosing
                && entry.IsPosted)
            .GroupBy(entry => entry.FinancialDocumentId)
            .Select(group => new { PeriodId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PeriodId, item => item.Count);

        return Ok(new
        {
            data = periods.Select(period => new
            {
                period.Id,
                period.Name,
                period.StartDate,
                period.EndDate,
                period.Status,
                period.ClosedAt,
                period.Notes,
                period.BranchId,
                IsAnnual = IsAnnualPeriod(period),
                ClosingEntryCount = closingEntryCounts.GetValueOrDefault(period.Id)
            })
        });
    }

    [HttpPost("accounting-periods")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateAccountingPeriod([FromBody] CreateAccountingPeriodRequest req)
    {
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty || string.IsNullOrWhiteSpace(req.Name) || req.StartDate == default || req.EndDate < req.StartDate)
            return BadRequest(new { message = "أدخل اسماً وفترة مالية صحيحة." });
        var overlaps = await db.AccountingPeriods.AnyAsync(period =>
            period.BranchId == branchId
            && period.StartDate <= req.EndDate
            && period.EndDate >= req.StartDate);
        if (overlaps)
            return Conflict(new { message = "الفترة الجديدة تتداخل مع فترة مالية موجودة." });

        var period = new AccountingPeriod
        {
            BranchId = branchId,
            Name = req.Name.Trim(),
            StartDate = req.StartDate,
            EndDate = req.EndDate,
            Notes = req.Notes?.Trim()
        };
        db.AccountingPeriods.Add(period);
        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Create, "AccountingPeriod", period.Id,
            details: $"{period.Name}: {period.StartDate:yyyy-MM-dd}..{period.EndDate:yyyy-MM-dd}");
        return CreatedAtAction(nameof(GetAccountingPeriods), new { id = period.Id }, period);
    }

    [HttpPost("accounting-periods/{id:guid}/close")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CloseAccountingPeriod(Guid id)
    {
        var period = await db.AccountingPeriods.FirstOrDefaultAsync(item => item.Id == id);
        if (period is null) return NotFound(new { message = "الفترة غير موجودة." });
        if (!currentUser.IsAdmin && period.BranchId != currentUser.BranchId) return Deny();
        if (period.Status == "Closed") return BadRequest(new { message = "الفترة مقفلة بالفعل." });

        var hasUnposted = await db.JournalEntries.AnyAsync(entry =>
            entry.BranchId == period.BranchId
            && entry.EntryDate >= period.StartDate
            && entry.EntryDate <= period.EndDate
            && !entry.IsPosted);
        if (hasUnposted)
            return BadRequest(new { message = "لا يمكن الإقفال: توجد قيود غير مرحلة داخل الفترة." });

        if (IsAnnualPeriod(period))
        {
            var hasOpenProfitAndLoss = await db.JournalLines.AnyAsync(line =>
                line.JournalEntry != null
                && line.JournalEntry.BranchId == period.BranchId
                && line.JournalEntry.EntryDate >= period.StartDate
                && line.JournalEntry.EntryDate <= period.EndDate
                && line.JournalEntry.IsPosted
                && line.JournalEntry.FinancialDocumentType != FinancialDocumentType.YearEndClosing
                && ProfitAndLossAccountTypes.Contains(line.AccountType));
            if (hasOpenProfitAndLoss)
                return BadRequest(new { message = "هذه سنة مالية كاملة وبها إيرادات أو مصروفات. استخدم الإقفال السنوي لإنشاء قيود الإقفال وفتح السنة التالية." });
        }

        period.Status = "Closed";
        period.ClosedAt = DateTime.UtcNow;
        period.ClosedBy = currentUser.UserId;
        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Update, "AccountingPeriod", period.Id, details: "Period closed");
        return Ok(new { message = "تم إقفال الفترة. أي قيد جديد بتاريخ داخلها سيُرفض." });
    }

    [HttpPost("accounting-periods/{id:guid}/year-end-close")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CloseFiscalYear(Guid id, [FromBody] YearEndCloseRequest req)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        if (userId == Guid.Empty)
            return BadRequest(new { message = "تعذر تحديد المستخدم الذي ينفذ الإقفال." });

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            if (db.Database.IsRelational())
            {
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock({0})",
                    StableLockKeyHelper.StableGuidToLong(id));
            }

            var period = await db.AccountingPeriods.FirstOrDefaultAsync(item => item.Id == id && item.IsActive);
            if (period is null)
                return NotFound(new { message = "الفترة غير موجودة." });
            if (!currentUser.IsAdmin && period.BranchId != currentUser.BranchId)
                return Deny();
            if (period.Status == "Closed")
                return BadRequest(new { message = "الفترة مقفلة بالفعل." });
            if (!IsAnnualPeriod(period))
                return BadRequest(new { message = "الإقفال السنوي يتطلب فترة مدتها سنة كاملة." });

            var alreadyClosed = await db.JournalEntries.AnyAsync(entry =>
                entry.FinancialDocumentType == FinancialDocumentType.YearEndClosing
                && entry.FinancialDocumentId == period.Id);
            if (alreadyClosed)
                return Conflict(new { message = "تم إنشاء قيود إقفال لهذه السنة بالفعل." });

            var hasUnposted = await db.JournalEntries.AnyAsync(entry =>
                entry.BranchId == period.BranchId
                && entry.EntryDate >= period.StartDate
                && entry.EntryDate <= period.EndDate
                && !entry.IsPosted);
            if (hasUnposted)
                return BadRequest(new { message = "لا يمكن الإقفال: توجد قيود غير مرحلة داخل السنة." });

            var periodEndExclusive = period.EndDate.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var hasOpenCashierSession = await db.CashierSessions.AnyAsync(session =>
                session.BranchId == period.BranchId
                && session.Status == SessionStatus.Open
                && session.OpeningTime < periodEndExclusive);
            if (hasOpenCashierSession)
                return BadRequest(new { message = "لا يمكن إقفال السنة قبل إقفال جميع ورديات الصندوق التابعة لها." });

            var nextStartDate = period.EndDate.AddDays(1);
            var nextEndDate = nextStartDate.AddYears(1).AddDays(-1);
            var overlapsNextPeriod = await db.AccountingPeriods.AnyAsync(item =>
                item.BranchId == period.BranchId
                && item.IsActive
                && item.StartDate <= nextEndDate
                && item.EndDate >= nextStartDate);
            if (overlapsNextPeriod)
                return Conflict(new { message = "تعذر فتح السنة التالية لوجود فترة مالية متداخلة معها." });

            var ledgerLines = await db.JournalLines
                .Where(line => line.JournalEntry != null
                    && line.JournalEntry.BranchId == period.BranchId
                    && line.JournalEntry.EntryDate >= period.StartDate
                    && line.JournalEntry.EntryDate <= period.EndDate
                    && line.JournalEntry.IsPosted
                    && line.JournalEntry.FinancialDocumentType != FinancialDocumentType.YearEndClosing
                    && ProfitAndLossAccountTypes.Contains(line.AccountType))
                .Select(line => new
                {
                    line.AccountType,
                    line.AccountId,
                    line.Debit,
                    line.Credit,
                    Currency = line.JournalEntry!.Currency,
                    line.JournalEntry.ExchangeRateToYer,
                    line.JournalEntry.EntryDate,
                    line.JournalEntry.CreatedAt
                })
                .ToListAsync();

            var closingEntries = new List<JournalEntry>();
            var results = new List<object>();
            foreach (var currencyGroup in ledgerLines
                .GroupBy(line => string.IsNullOrWhiteSpace(line.Currency) ? "YER" : line.Currency.Trim().ToUpperInvariant())
                .OrderBy(group => group.Key))
            {
                var closingLines = currencyGroup
                    .GroupBy(line => new { line.AccountType, line.AccountId })
                    .Select(group => new
                    {
                        group.Key.AccountType,
                        group.Key.AccountId,
                        Balance = group.Sum(line => line.Debit - line.Credit)
                    })
                    .Where(item => Math.Abs(item.Balance) >= 0.005m)
                    .Select(item => new JournalLine
                    {
                        AccountType = item.AccountType,
                        AccountId = item.AccountId,
                        Debit = item.Balance < 0 ? Math.Abs(item.Balance) : 0m,
                        Credit = item.Balance > 0 ? item.Balance : 0m,
                        BranchId = period.BranchId,
                        Description = $"إقفال {item.AccountType} للسنة {period.Name}"
                    })
                    .ToList();

                if (closingLines.Count == 0)
                    continue;

                var totalDebit = closingLines.Sum(line => line.Debit);
                var totalCredit = closingLines.Sum(line => line.Credit);
                var equityDebit = totalCredit > totalDebit ? totalCredit - totalDebit : 0m;
                var equityCredit = totalDebit > totalCredit ? totalDebit - totalCredit : 0m;
                if (equityDebit > 0 || equityCredit > 0)
                {
                    closingLines.Add(new JournalLine
                    {
                        AccountType = JournalAccountType.OwnerEquity,
                        AccountId = period.BranchId,
                        Debit = equityDebit,
                        Credit = equityCredit,
                        BranchId = period.BranchId,
                        Description = $"صافي نتيجة السنة المالية {period.Name}"
                    });
                }

                var exchangeRate = currencyGroup
                    .OrderByDescending(line => line.EntryDate)
                    .ThenByDescending(line => line.CreatedAt)
                    .Select(line => line.ExchangeRateToYer)
                    .FirstOrDefault();
                if (currencyGroup.Key == "YER") exchangeRate = 1m;
                if (exchangeRate <= 0)
                    return BadRequest(new { message = $"لا يمكن إقفال {currencyGroup.Key}: لا يوجد سعر صرف تاريخي صالح." });

                var entry = new JournalEntry
                {
                    EntryNumber = await journalEntryService.GenerateEntryNumberAsync(),
                    FinancialDocumentId = period.Id,
                    FinancialDocumentType = FinancialDocumentType.YearEndClosing,
                    Description = $"قيد إقفال السنة المالية {period.Name} - {currencyGroup.Key}",
                    EntryDate = period.EndDate,
                    Currency = currencyGroup.Key,
                    ExchangeRateToYer = exchangeRate,
                    BranchId = period.BranchId,
                    PerformedBy = userId,
                    IsPosted = true,
                    PostedAt = DateTime.UtcNow,
                    Lines = closingLines
                };
                db.JournalEntries.Add(entry);
                await db.SaveChangesAsync();
                closingEntries.Add(entry);
                results.Add(new
                {
                    entry.Id,
                    entry.EntryNumber,
                    entry.Currency,
                    NetIncome = equityCredit - equityDebit,
                    TotalDebit = entry.Lines.Sum(line => line.Debit),
                    TotalCredit = entry.Lines.Sum(line => line.Credit)
                });
            }

            var nextPeriod = new AccountingPeriod
            {
                BranchId = period.BranchId,
                Name = string.IsNullOrWhiteSpace(req.NextPeriodName)
                    ? $"السنة المالية {nextStartDate.Year}"
                    : req.NextPeriodName.Trim(),
                StartDate = nextStartDate,
                EndDate = nextEndDate,
                Status = "Open",
                Notes = $"فُتحت تلقائياً بعد إقفال {period.Name}"
            };
            db.AccountingPeriods.Add(nextPeriod);

            period.Status = "Closed";
            period.ClosedAt = DateTime.UtcNow;
            period.ClosedBy = userId;
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            await audit.LogAsync(AuditAction.Update, "AccountingPeriod", period.Id,
                details: $"Fiscal year closed; entries={closingEntries.Count}; nextPeriod={nextPeriod.Id}");

            return Ok(new
            {
                message = "تم إقفال السنة وترحيل صافي النتيجة وفتح السنة التالية بنجاح.",
                PeriodId = period.Id,
                ClosingEntries = results,
                NextPeriod = new { nextPeriod.Id, nextPeriod.Name, nextPeriod.StartDate, nextPeriod.EndDate }
            });
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}
