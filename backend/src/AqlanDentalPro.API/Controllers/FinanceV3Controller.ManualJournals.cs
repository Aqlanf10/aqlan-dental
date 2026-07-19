using AqlanDentalPro.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    public sealed class CreateManualJournalEntryRequest
    {
        public string Description { get; init; } = string.Empty;
        public DateOnly EntryDate { get; init; }
        public string Currency { get; init; } = "YER";
        public decimal? ExchangeRateToYer { get; init; }
        public List<ManualJournalLineRequest> Lines { get; init; } = [];
    }

    public sealed class ManualJournalLineRequest
    {
        public string AccountType { get; init; } = string.Empty;
        public Guid? AccountId { get; init; }
        public decimal Debit { get; init; }
        public decimal Credit { get; init; }
        public string? Description { get; init; }
    }

    /// <summary>
    /// Records a posted manual accrual entry. Treasury lines are intentionally excluded:
    /// cash movement must use the dedicated receipt, disbursement, or transfer workflow.
    /// </summary>
    [HttpPost("journal-entries/manual")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateManualJournalEntry([FromBody] CreateManualJournalEntryRequest req)
    {
        var branchId = await ResolveBranchIdAsync();
        var userId = currentUser.UserId ?? Guid.Empty;
        if (branchId == Guid.Empty || userId == Guid.Empty)
            return BadRequest(new { message = "تعذر تحديد الفرع أو المستخدم لترحيل القيد." });
        if (string.IsNullOrWhiteSpace(req.Description) || req.Description.Trim().Length > 500)
            return BadRequest(new { message = "بيان القيد مطلوب ولا يتجاوز 500 حرف." });
        if (req.EntryDate == default)
            return BadRequest(new { message = "تاريخ القيد مطلوب." });
        if (req.Lines.Count < 2)
            return BadRequest(new { message = "القيد اليدوي يجب أن يحتوي على بندين على الأقل." });

        var currency = req.Currency.Trim().ToUpperInvariant();
        if (currency is not ("YER" or "SAR" or "USD"))
            return BadRequest(new { message = "العملة يجب أن تكون YER أو SAR أو USD." });
        var exchangeRate = currency == "YER" ? 1m : req.ExchangeRateToYer.GetValueOrDefault();
        if (exchangeRate <= 0)
            return BadRequest(new { message = "سعر الصرف إلى الريال اليمني مطلوب للعملات الأجنبية." });

        var lines = new List<(JournalAccountType accountType, Guid accountId, decimal debit, decimal credit, string? description)>();
        foreach (var line in req.Lines)
        {
            if (!Enum.TryParse<JournalAccountType>(line.AccountType, true, out var accountType))
                return BadRequest(new { message = $"نوع حساب غير صالح: {line.AccountType}." });
            if (accountType == JournalAccountType.Treasury)
                return BadRequest(new { message = "لا يُسمح بالقيد اليدوي على الخزينة. استخدم سند قبض أو سند صرف أو تحويل خزينة." });
            if (line.Debit < 0 || line.Credit < 0 || (line.Debit > 0 && line.Credit > 0) || (line.Debit == 0 && line.Credit == 0))
                return BadRequest(new { message = "كل بند يجب أن يكون مديناً أو دائناً فقط وبمبلغ أكبر من صفر." });

            var accountId = branchId;
            if (accountType is JournalAccountType.Payable or JournalAccountType.AccountsPayable)
            {
                var supplierId = line.AccountId;
                if (!supplierId.HasValue || !await db.Suppliers.AnyAsync(s => s.Id == supplierId.Value))
                    return BadRequest(new { message = "بند الذمم الدائنة يتطلب مورداً صالحاً." });
                accountId = supplierId.Value;
            }
            else if (accountType is JournalAccountType.PatientReceivable or JournalAccountType.PatientAdvance)
            {
                var patientId = line.AccountId;
                if (!patientId.HasValue || !await db.Patients.AnyAsync(p => p.Id == patientId.Value && p.BranchId == branchId))
                    return BadRequest(new { message = "بند ذمم المرضى يتطلب مريضاً من نفس الفرع." });
                accountId = patientId.Value;
            }

            lines.Add((accountType, accountId, line.Debit, line.Credit, line.Description?.Trim()));
        }

        if (lines.Sum(l => l.debit) != lines.Sum(l => l.credit))
            return BadRequest(new { message = "القيد غير متوازن: إجمالي المدين يجب أن يساوي إجمالي الدائن." });

        var entry = await journalEntryService.CreateEntryAsync(
            FinancialDocumentType.Other,
            Guid.NewGuid(),
            req.Description.Trim(),
            req.EntryDate,
            branchId,
            userId,
            cashierSessionId: null,
            treasuryId: null,
            lines,
            autoSave: false);
        entry.Currency = currency;
        entry.ExchangeRateToYer = exchangeRate;
        entry.IsPosted = true;
        entry.PostedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Create, "ManualJournalEntry", entry.Id,
            details: $"Manual posted entry {entry.EntryNumber}; currency={currency}; debit={lines.Sum(l => l.debit)}");

        return CreatedAtAction(nameof(GetJournalEntryById), new { id = entry.Id }, new
        {
            entry.Id,
            entry.EntryNumber,
            entry.EntryDate,
            entry.Currency,
            entry.ExchangeRateToYer,
            TotalDebit = lines.Sum(l => l.debit),
            TotalCredit = lines.Sum(l => l.credit),
        });
    }
}
