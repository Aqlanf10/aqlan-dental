using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

public partial class FinanceV3Controller
{
    /// <summary>
    /// POST /api/finance-v3/treasuries — Create a treasury account (Admin only).
    /// Reuses logic from TreasuriesController.Create.
    /// </summary>
    [HttpPost("treasuries")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> CreateTreasury([FromBody] CreateTreasuryRequest req)
    {
        if (!await CanAsync("finance.treasuries", "create")) return Deny();
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "اسم الخزنة/الحساب مطلوب" });
        if (!Enum.TryParse<TreasuryType>(req.Type, true, out var type))
            return BadRequest(new { message = "نوع الخزنة غير صالح. المتاح: Vault أو Bank" });
        if (req.OpeningBalance < 0)
            return BadRequest(new { message = "رصيد البداية لا يمكن أن يكون سالباً" });

        var currency = NormalizeTreasuryCurrency(req.Currency);

        // Sprint 1: Admin branchId fallback
        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. لا توجد فروع نشطة في النظام." });

        var treasury = new Treasury
        {
            Name = req.Name.Trim(),
            Type = type,
            Currency = currency,
            Balance = req.OpeningBalance,
            BranchId = branchId,
            IsActive = true
        };
        db.Treasuries.Add(treasury);

        if (req.OpeningBalance > 0)
        {
            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var cashflow = new CashFlowTransaction
            {
                TransactionNumber = $"TX-{datePart}-IN-OP-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                Type = TransactionType.Inflow,
                Category = FinancialCategory.InternalTransfer,
                Amount = req.OpeningBalance,
                Currency = currency,
                PaymentMethod = type == TreasuryType.Bank ? "bank" : "cash",
                TransactionDate = ClinicTimeProvider.ClinicToday(),
                ReferenceId = treasury.Id,
                ReferenceNumber = "OP-BAL",
                Description = $"رصيد افتتاحي لبداية تشغيل {treasury.Name}",
                PerformedBy = currentUser.UserId ?? Guid.Empty,
                BranchId = branchId
            };
            db.CashFlowTransactions.Add(cashflow);

            // Fix: Create JournalEntry manually with IsPosted = true from the start.
            // This avoids the double-save problem where CreateEntryAsync calls SaveChanges
            // with IsPosted=false, then IsPosted=true is set and saved again — if the second
            // save fails, the entry remains unposted, meaning the opening balance is in
            // CashFlowTransaction but not in JournalLine. By creating everything in memory
            // and saving once, we ensure atomicity.
            var entryNumber = await journalEntryService.GenerateEntryNumberAsync();
            var openingJe = new JournalEntry
            {
                EntryNumber = entryNumber,
                FinancialDocumentId = treasury.Id,
                FinancialDocumentType = FinancialDocumentType.VaultTransfer,
                Description = $"رصيد افتتاحي: {treasury.Name}",
                EntryDate = ClinicTimeProvider.ClinicToday(),
                BranchId = branchId,
                PerformedBy = currentUser.UserId ?? Guid.Empty,
                CashierSessionId = null,
                TreasuryId = treasury.Id,
                Currency = currency,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                IsReversal = false,
            };
            db.JournalEntries.Add(openingJe);

            // Debit: Treasury (asset increase)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = openingJe.Id,
                AccountType = JournalAccountType.Treasury,
                AccountId = treasury.Id,
                Debit = req.OpeningBalance,
                Credit = 0m,
                Description = "رصيد افتتاحي خزينة",
                BranchId = branchId,
            });

            // Credit: OwnerEquity (source of funds)
            db.JournalLines.Add(new JournalLine
            {
                JournalEntryId = openingJe.Id,
                AccountType = JournalAccountType.OwnerEquity,
                AccountId = branchId,
                Debit = 0m,
                Credit = req.OpeningBalance,
                Description = "رصيد افتتاحي — حقوق الملكية",
                BranchId = branchId,
            });
        }

        await db.SaveChangesAsync();
        await audit.LogAsync(AuditAction.Create, "Treasury", treasury.Id,
            details: $"Created treasury '{treasury.Name}' (type={treasury.Type}, balance={treasury.Balance:N0}, branch={branchId})");

        return Ok(new { treasury.Id, treasury.Name, Type = treasury.Type.ToString(), treasury.Currency, treasury.Balance, message = "تم إنشاء الخزنة/الحساب المالي بنجاح" });
    }
    /// <summary>
    /// POST /api/finance-v3/vault-transfers — Create a vault transfer.
    /// Reuses logic from VaultTransfersController.Create.
    /// </summary>
    [HttpPost("vault-transfers")]
    [Authorize(Policy = "FinanceWrite")]
    public async Task<IActionResult> CreateVaultTransfer([FromBody] CreateTransferRequest req)
    {
        if (!await CanAsync("finance.treasuries", "create")) return Deny();
        if (req.Amount <= 0)
            return BadRequest(new { message = "يجب أن يكون مبلغ التحويل أكبر من الصفر" });
        // Sprint 1: Admin branchId fallback
        if (!currentUser.IsAdmin && (!currentUser.BranchId.HasValue || currentUser.BranchId.Value == Guid.Empty))
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. يرجى تسجيل الدخول بفرع صالح." });

        var branchId = await ResolveBranchIdAsync();
        if (branchId == Guid.Empty)
            return BadRequest(new { message = "لم يتم تحديد فرع للمستخدم. لا توجد فروع نشطة في النظام." });
        var userId = currentUser.UserId ?? Guid.Empty;

        // C-07 V3 FIX: Move the source treasury load + balance check INSIDE the transaction
        // (with FOR UPDATE on PostgreSQL) so two concurrent transfers cannot both pass the
        // balance check and then both deduct (which would drive Treasury.Balance negative).
        // Previously the balance check was performed BEFORE BeginTransactionAsync, the
        // advisory lock was on the transfer-number sequence (not on the source treasury row),
        // and the deduction used tracked-entity mutation (no atomic conditional SQL).
        // The legacy VaultTransfersController.Create path was already fixed (C-07); this V3
        // path now mirrors that pattern.

        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            var lockKey = StableLockKeyHelper.VaultTransferNumber;
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockKey);

            // Load destination treasury inside the tx (existence check, no balance mutation).
            var destTreasury = await db.Treasuries.FirstOrDefaultAsync(t => t.Id == req.DestinationTreasuryId && t.BranchId == branchId && t.IsActive);
            if (destTreasury == null)
                return BadRequest(new { message = "الخزنة المستهدفة غير موجودة أو غير تابعة للفرع" });

            // Load source treasury inside the tx with FOR UPDATE on PostgreSQL so the row is
            // locked until commit. On InMemory (tests), this falls back to a plain load.
            Treasury? sourceTreasury = null;
            if (req.SourceTreasuryId.HasValue)
            {
                if (db.Database.IsRelational())
                {
                    // FOR UPDATE on the source treasury row (PostgreSQL). Acquired within the
                    // advisory-lock transaction so concurrent transfers serialize on the row.
                    await db.Database.ExecuteSqlRawAsync(
                        "SELECT 1 FROM \"Treasuries\" WHERE \"Id\" = {0} FOR UPDATE",
                        req.SourceTreasuryId.Value);
                }

                sourceTreasury = await db.Treasuries.FirstOrDefaultAsync(t => t.Id == req.SourceTreasuryId.Value && t.BranchId == branchId && t.IsActive);
                if (sourceTreasury == null)
                    return BadRequest(new { message = "الخزنة المصدر غير موجودة أو غير تابعة للفرع" });

                if (!string.Equals(sourceTreasury.Currency, destTreasury.Currency, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "لا يمكن التحويل المباشر بين خزائن بعملات مختلفة. استخدم عملية مصارفة مستقلة بسعر صرف موثق." });

                // AUTHORITATIVE re-check inside the lock — concurrent transfer may have
                // already deducted enough to make this transfer impossible.
                if (sourceTreasury.Balance < req.Amount)
                    return BadRequest(new { message = $"عذراً، رصيد الخزنة المصدر ({sourceTreasury.Balance:N0} ر.ي) أقل من مبلغ التحويل المطلوب ({req.Amount:N0} ر.ي)" });
            }

            var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"TR-{datePart}-";
            var lastTransfer = await db.VaultTransfers.IgnoreQueryFilters()
                .Where(t => t.TransferNumber.StartsWith(prefix))
                .OrderByDescending(t => t.TransferNumber).Select(t => t.TransferNumber).FirstOrDefaultAsync();
            var nextSeq = 1;
            if (!string.IsNullOrEmpty(lastTransfer) && lastTransfer.Length > prefix.Length)
            {
                var seqPart = lastTransfer[prefix.Length..];
                if (int.TryParse(seqPart, out var lastSeq)) nextSeq = lastSeq + 1;
            }
            var transferNumber = $"{prefix}{nextSeq:D3}";

            var activeSession = await db.CashierSessions.FirstOrDefaultAsync(s => s.CashierId == userId && s.Status == SessionStatus.Open && s.IsActive);

            var transfer = new VaultTransfer
            {
                TransferNumber = transferNumber,
                SourceTreasuryId = req.SourceTreasuryId,
                DestinationTreasuryId = req.DestinationTreasuryId,
                CashierSessionId = activeSession?.Id,
                Amount = req.Amount,
                TransferDate = DateTime.UtcNow,
                PerformedBy = userId,
                Status = TransferStatus.Pending,
                Notes = req.Notes?.Trim(),
                DepositSource = req.DepositSource
            };

            // Deduct inside the tx. With xmin concurrency token on Treasury (DB-02) and the
            // FOR UPDATE row lock above, a concurrent transfer that already mutated this row
            // will trigger DbUpdateConcurrencyException, which we rethrow as 409.
            if (sourceTreasury != null) sourceTreasury.Balance -= req.Amount;

            db.VaultTransfers.Add(transfer);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            await audit.LogAsync(AuditAction.Create, "VaultTransfer", transfer.Id);
            return Ok(new { transfer.Id, transfer.TransferNumber, transfer.Amount, Status = transfer.Status.ToString(), message = "تم إنشاء طلب ترحيل السيولة بنجاح وهو قيد المراجعة والاستلام الفعلي" });
        }
        catch (DbUpdateConcurrencyException)
        {
            // DB-02: xmin concurrency token on Treasury detected a concurrent edit.
            await tx.RollbackAsync();
            return Conflict(new { message = "تم تعديل رصيد الخزنة من قبل مستخدم آخر، يرجى التحديث والمحاولة مرة أخرى" });
        }
        catch { await tx.RollbackAsync(); throw; }
    }
    /// <summary>
    /// POST /api/finance-v3/treasuries/{id}/recalculate — Recalculate treasury balance (Admin only).
    /// Migration C: Balance now recalculated from JournalLine (Treasury account type)
    /// instead of CashFlowTransaction. Treasury balance = SUM(Debit) - SUM(Credit)
    /// for all posted JournalLines where AccountType == Treasury and AccountId matches.
    /// In double-entry: Treasury Debit = inflow (increase), Credit = outflow (decrease).
    ///
    /// Fix: Includes a fallback for opening balances created before the JournalEntry
    /// migration. If no opening-balance JournalEntry exists for this treasury, the
    /// opening amount from CashFlowTransaction (ReferenceNumber = "OP-BAL") is added.
    /// </summary>
    [HttpPost("treasuries/{id:guid}/recalculate")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RecalculateTreasuryBalance(Guid id)
    {
        if (!await CanAsync("finance.treasuries", "edit")) return Deny();
        var treasury = await db.Treasuries.FindAsync(id);
        if (treasury == null || !treasury.IsActive)
            return NotFound(new { message = "الخزنة غير موجودة" });

        var oldBalance = treasury.Balance;

        // Migration C: Recalculate from JournalLine instead of CashFlowTransaction
        // Balance = SUM(Debit) - SUM(Credit) for Treasury lines pointing to this treasury
        var journalBalance = await db.JournalLines
            .Where(l => l.AccountType == JournalAccountType.Treasury
                && l.AccountId == treasury.Id
                && l.JournalEntry.IsPosted
                && l.JournalEntry.BranchId == treasury.BranchId)
            .SumAsync(l => (decimal?)(l.Debit - l.Credit)) ?? 0m;

        // Fix: Fallback for opening balances created before the JournalEntry migration.
        // Search specifically for the opening balance JournalEntry for this treasury.
        // Opening balance = VaultTransfer entry with description containing "رصيد افتتاحي"
        // and a Treasury Debit line pointing to this treasury.
        // NOTE: This depends on the description pattern used when creating treasury opening
        // balances. If the description format changes, this check must be updated.
        var hasOpeningJournalEntry = await db.JournalEntries
            .AnyAsync(je => je.FinancialDocumentType == FinancialDocumentType.VaultTransfer
                && je.IsPosted
                && je.Description.Contains("رصيد افتتاحي")
                && je.Lines.Any(l => l.AccountType == JournalAccountType.Treasury
                    && l.AccountId == treasury.Id));

        decimal openingBalanceFromCashFlow = 0m;
        if (!hasOpeningJournalEntry)
        {
            // Legacy treasuries created before Migration C may only have a CashFlowTransaction
            // with ReferenceNumber = "OP-BAL" for the opening balance
            openingBalanceFromCashFlow = await db.CashFlowTransactions
                .Where(c => c.TreasuryId == treasury.Id
                    && c.ReferenceNumber == "OP-BAL"
                    && c.Category == FinancialCategory.InternalTransfer
                    && !c.IsReversal
                    && c.IsActive)
                .SumAsync(c => (decimal?)(c.Type == TransactionType.Inflow ? c.Amount : -c.Amount)) ?? 0m;
        }

        // If an opening JournalEntry exists, journalBalance already includes it.
        // If not (legacy treasury), add the CashFlow opening balance as a correction.
        var calculatedBalance = journalBalance;
        if (!hasOpeningJournalEntry && openingBalanceFromCashFlow != 0m)
        {
            calculatedBalance += openingBalanceFromCashFlow;
            logger.LogInformation(
                "Treasury {TreasuryId}: Applied opening balance fallback from CashFlowTransaction = {Fallback:F2}",
                treasury.Id, openingBalanceFromCashFlow);
        }

        var drift = calculatedBalance - oldBalance;

        if (drift != 0)
            logger.LogWarning("Treasury drift detected for {TreasuryId} ({Name}): Old={OldBalance}, New={NewBalance}, Drift={Drift}", treasury.Id, treasury.Name, oldBalance, calculatedBalance, drift);

        treasury.Balance = calculatedBalance;
        treasury.UpdatedAt = DateTime.UtcNow;

        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { message = "تعارض في تحديث رصيد الخزينة. يرجى المحاولة مرة أخرى." }); }

        await audit.LogAsync(AuditAction.Update, "Treasury", id, details: $"Recalculated via V3 (JournalLine): old={oldBalance}, new={calculatedBalance}, drift={drift}, openingFallback={openingBalanceFromCashFlow}");

        return Ok(new { treasury.Id, treasury.Name, OldBalance = oldBalance, NewBalance = calculatedBalance, Drift = drift, DriftDetected = drift != 0, OpeningFallback = openingBalanceFromCashFlow, message = drift != 0 ? $"تم إعادة حساب الرصيد. تم اكتشاف انحراف بمبلغ {drift:N0} ر.ي" : "تم إعادة حساب الرصيد. لا يوجد انحراف" });
    }

    private static string NormalizeTreasuryCurrency(string? currency)
    {
        var code = string.IsNullOrWhiteSpace(currency) ? "YER" : currency.Trim().ToUpperInvariant();
        return code is "YER" or "SAR" or "USD"
            ? code
            : throw new ArgumentException("العملة يجب أن تكون YER أو SAR أو USD");
    }
}
