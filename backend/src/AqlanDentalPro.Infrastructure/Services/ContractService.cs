using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Contract service — extracted from <c>FinanceService</c> as part of TD-021 PR A3.
/// Owns the read + clean-write side of the contracts cluster.
///
/// This is a pure code move (no logic change). The previous implementation lived in
/// <c>FinanceService.GetContractsAsync</c>, <c>GetContractByIdAsync</c>, and
/// <c>UpdateContractAsync</c>.
///
/// <see cref="MapContractList"/> was a private static helper in FinanceService and
/// moved here as a private static helper (only used by GetContractsAsync).
///
/// The entangled contract methods (<c>CreateContractAsync</c>,
/// <c>UpdateContractStatusAsync</c>, <c>TryReconcileContractStatusAsync</c>) stay
/// in FinanceService because they depend on payment-side helpers. They will move
/// naturally when PR A4 extracts the PaymentService cluster.
/// </summary>
public class ContractService(
    AppDbContext db,
    ICurrentUserService currentUser,
    ILogger<ContractService> logger,
    IJournalEntryService journalEntryService,
    IPaymentService paymentService) : IContractService
{
    public async Task<List<ContractListDto>> GetContractsAsync(int page, int pageSize, Guid? patientId, string? status)
    {
        var branchId = currentUser.IsAdmin ? (Guid?)null : currentUser.BranchId;

        var query = db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
            .Include(c => c.Package) // YOLO-S2: package name + color for display
            .AsQueryable();

        if (branchId.HasValue) query = query.Where(c => c.Patient.BranchId == branchId.Value);
        if (patientId.HasValue) query = query.Where(c => c.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ContractStatus>(status, true, out var contractStatus))
            query = query.Where(c => c.Status == contractStatus);

        return await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => MapContractList(c))
            .ToListAsync();
    }

    public async Task<ContractDetailDto?> GetContractByIdAsync(Guid id)
    {
        var c = await db.Contracts
            .Include(c => c.Patient)
            .Include(c => c.Payments)
                .ThenInclude(p => p.Doctor)
            .Include(c => c.Package) // YOLO-S2: package name + color for display
            .FirstOrDefaultAsync(c => c.Id == id);

        if (c == null) return null;

        var dto = new ContractDetailDto
        {
            Id = c.Id,
            PatientId = c.PatientId,
            // Patient may be null here even though the FK is required: EF Core's global
            // soft-delete query filter applies to Include-d navigations too, so a contract
            // whose patient was later soft-deleted loads with Patient == null (LEFT JOIN
            // filtered out), not an excluded row. Never crash a financial record over this.
            PatientName = c.Patient != null ? $"{c.Patient.FirstName} {c.Patient.LastName}" : "مريض محذوف",
            PatientNumber = c.Patient?.PatientNumber ?? "—",
            Specialty = c.Specialty,
            Currency = FinanceMappers.NormalizeCurrency(c.Currency),
            TotalAmount = c.TotalAmount,
            DownPayment = c.DownPayment,
            PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
            RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
            InstallmentsCount = c.InstallmentsCount,
            InstallmentAmount = c.InstallmentAmount,
            StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
            Status = c.Status.ToString(),
            DiscountAmount = c.DiscountAmount,
            DiscountReason = c.DiscountReason,
            Notes = c.Notes,
            Payments = c.Payments.OrderByDescending(p => p.PaymentDate).Select(FinanceMappers.MapPayment).ToList(),
            // YOLO-S2: package link
            PackageId = c.PackageId,
            PackageName = c.Package?.Name,
            PackageColor = c.Package?.Color,
        };

        return dto;
    }

    public async Task<ContractDetailDto?> UpdateContractAsync(Guid id, UpdateContractRequest req)
    {
        var contract = await db.Contracts.FindAsync(id);
        if (contract == null) return null;

        // Phase 0B: Validate that TotalAmount is not reduced below what's already been paid
        {
            var alreadyPaid = await db.Payments
                .Where(p => p.ContractId == id && p.IsActive)
                .SumAsync(p => (decimal?)(p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount)) ?? 0m;
            if (req.TotalAmount < alreadyPaid)
                throw new ArgumentException($"لا يمكن تقليل إجمالي العقد ({req.TotalAmount:N0} ر.ي) إلى أقل من المبلغ المدفوع فعلياً ({alreadyPaid:N0} ر.ي).");
        }

        contract.Specialty        = req.Specialty;
        contract.Currency         = FinanceMappers.NormalizeCurrency(req.Currency ?? contract.Currency);
        contract.TotalAmount      = req.TotalAmount;
        contract.InstallmentsCount = req.InstallmentsCount;
        contract.InstallmentAmount = req.InstallmentAmount;
        contract.StartDate        = req.StartDate != null ? DateOnly.Parse(req.StartDate) : contract.StartDate;
        contract.DiscountAmount   = req.DiscountAmount;
        contract.DiscountReason   = req.DiscountReason;
        contract.Notes            = req.Notes;
        contract.UpdatedAt        = DateTime.UtcNow;

        // YOLO-S2: update the package link. Resolve Guid.Empty → null ("clear").
        // null on the request body leaves the existing value unchanged (PATCH semantics).
        if (req.PackageId.HasValue)
        {
            var newPkgId = req.PackageId.Value == Guid.Empty ? null : (Guid?)req.PackageId.Value;
            if (newPkgId.HasValue && newPkgId != contract.PackageId)
            {
                var pkgExists = await db.TreatmentPackages.AnyAsync(p => p.Id == newPkgId.Value && p.IsActive);
                if (!pkgExists)
                    throw new ArgumentException("الباقة المحددة غير موجودة أو معطّلة");
            }
            contract.PackageId = newPkgId;
        }

        await db.SaveChangesAsync();
        return await GetContractByIdAsync(id);
    }

    // ─── TD-021 A4 final slice: moved verbatim from FinanceService ──────────

    public async Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req)
    {
        await ActivePatientWriteGuard.EnsureAsync(db, req.PatientId);

        // YOLO-S2: validate the optional package link if provided. Resolves Guid.Empty to null
        // so the caller can send an empty Guid to mean "no package" without a separate flag.
        Guid? packageId = req.PackageId;
        if (packageId == Guid.Empty) packageId = null;
        if (packageId.HasValue)
        {
            var pkgExists = await db.TreatmentPackages.AnyAsync(p => p.Id == packageId.Value && p.IsActive);
            if (!pkgExists)
                throw new ArgumentException("الباقة المحددة غير موجودة أو معطّلة");
        }

        var contract = new Contract
        {
            PatientId = req.PatientId,
            Specialty = req.Specialty,
            RelatedCaseId = req.RelatedCaseId,
            Currency = FinanceMappers.NormalizeCurrency(req.Currency),
            TotalAmount = req.TotalAmount,
            DownPayment = req.DownPayment,
            InstallmentsCount = req.InstallmentsCount,
            InstallmentAmount = req.InstallmentAmount,
            StartDate = req.StartDate != null ? DateOnly.Parse(req.StartDate) : ClinicTimeProvider.ClinicToday(),
            DiscountAmount = req.DiscountAmount,
            DiscountReason = req.DiscountReason,
            Status = ContractStatus.Active,
            Notes = req.Notes,
            CreatedBy = currentUser.UserId,
            PackageId = packageId, // YOLO-S2
        };

        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        // Auto-create down payment if specified
        if (req.DownPayment > 0)
        {
            await paymentService.CreatePaymentAsync(new CreatePaymentRequest
            {
                PatientId = req.PatientId,
                ContractId = contract.Id,
                Amount = req.DownPayment,
                Currency = FinanceMappers.NormalizeCurrency(req.Currency),
                AccountCurrency = FinanceMappers.NormalizeCurrency(req.Currency),
                PaymentMethod = req.DownPaymentMethod ?? "cash", // Sprint Patient-Finance-Ledger: was hardcoded "cash"
                ServiceDescription = "دفعة أولى"
            });
        }

        return (await GetContractByIdAsync(contract.Id))!;
    }

    public async Task<ContractDetailDto?> UpdateContractStatusAsync(Guid id, string status)
    {
        if (!Enum.TryParse<ContractStatus>(status, true, out var contractStatus)) return null;

        var contract = await db.Contracts
            .Include(c => c.Payments)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contract == null) return null;

        contract.Status    = contractStatus;
        contract.UpdatedAt = DateTime.UtcNow;

        // H8 FIX: When cancelling a contract with active payments, soft-delete
        // all linked payments to prevent financial reports from including
        // payments for cancelled contracts. Previously, cancelling a contract
        // left its payments active, causing incorrect balance calculations.
        if (contractStatus == ContractStatus.Cancelled)
        {
            // Finance Transaction Safety: Wrap the entire cancellation in a single
            // atomic transaction so that treasury balance, CashFlow reversals,
            // JournalEntry reversals, payment deactivation, and contract status
            // are committed together or rolled back together.
            // Previously, UpdateTreasuryBalanceAsync called SaveChangesAsync
            // independently, and DualWriteReversalEntryAsync also called
            // SaveChangesAsync, causing partial commits if a later step failed.
            var useTx = db.Database.IsRelational();
            var tx = useTx ? await db.Database.BeginTransactionAsync() : null;
            try
            {
                var activePayments = contract.Payments.Where(p => p.IsActive).ToList();
                foreach (var payment in activePayments)
                {
                    payment.IsActive = false;
                    payment.DeletedAt = DateTime.UtcNow;
                }

                // C3: Instead of soft-deleting linked CashFlowTransactions, create reversal entries.
                // CashFlowTransaction entries are immutable — they MUST NEVER be soft-deleted for
                // financial ledger integrity.
                foreach (var payment in activePayments)
                {
                    var linkedCashflow = await db.CashFlowTransactions
                        .FirstOrDefaultAsync(t => t.ReferenceId == payment.Id && t.Category == FinancialCategory.PatientPayment && t.IsActive);
                    if (linkedCashflow != null)
                    {
                        var reversalCashflow = new CashFlowTransaction
                        {
                            TransactionNumber = $"TX-{DateTime.UtcNow:yyyyMMdd}-REV-{Guid.NewGuid().ToString()[..8].ToUpper()}",
                            Type = linkedCashflow.Type == TransactionType.Inflow ? TransactionType.Outflow : TransactionType.Inflow,
                            Category = FinancialCategory.Reversal,
                            Amount = linkedCashflow.Amount,
                            PaymentMethod = linkedCashflow.PaymentMethod,
                            TransactionDate = ClinicTimeProvider.ClinicToday(),
                            ReferenceId = payment.Id,
                            ReferenceNumber = linkedCashflow.ReferenceNumber,
                            Description = $"قيد عكسي لإلغاء عقد - {linkedCashflow.Description}",
                            PerformedBy = currentUser.UserId ?? Guid.Empty,
                            BranchId = linkedCashflow.BranchId,
                            CashierSessionId = linkedCashflow.CashierSessionId,
                            IsReversal = true,
                            ReversalOfTransactionId = linkedCashflow.Id
                        };
                        db.CashFlowTransactions.Add(reversalCashflow);

                        // Link original to the reversal
                        linkedCashflow.ReversedByTransactionId = reversalCashflow.Id;
                        // Keep original's IsActive = true (never soft-delete CashFlowTransactions)
                    }
                    // Reverse treasury balance — use NoSave variant inside the transaction
                    await FinanceLedgerWriter.UpdateTreasuryBalanceNoSaveAsync(db, payment.BranchId ?? Guid.Empty, -payment.Amount, payment.PaymentMethod);

                    // Finance V3: Dual-write reversal entry for each reversed payment
                    // DualWriteReversalEntryAsync calls SaveChangesAsync internally,
                    // which persists all tracked changes within the current transaction.
                    await FinanceLedgerWriter.DualWriteReversalEntryAsync(db, journalEntryService, logger, currentUser.UserId ?? Guid.Empty, payment.Id, "إلغاء عقد");
                }

                // Persist all tracked changes (contract status, payment deactivations,
                // CashFlow reversals, treasury balance, JournalEntry reversals)
                await db.SaveChangesAsync();

                // H8 FIX: Re-evaluate linked invoice statuses after cancelling payments.
                // TryMarkInvoicePaidAsync calls SaveChangesAsync internally, but since
                // we are still inside the same transaction, it persists within the tx scope.
                var affectedInvoiceIds = activePayments
                    .Where(p => p.InvoiceId.HasValue)
                    .Select(p => p.InvoiceId!.Value)
                    .Distinct()
                    .ToList();

                foreach (var invoiceId in affectedInvoiceIds)
                {
                    try { await paymentService.TryMarkInvoicePaidAsync(invoiceId); }
                    catch (Exception ex) { logger.LogWarning(ex, "H8: Failed to re-evaluate invoice {InvoiceId} after contract cancellation", invoiceId); }
                }

                if (useTx) await tx!.CommitAsync();
            }
            catch
            {
                if (useTx) await tx!.RollbackAsync();
                throw;
            }
        }
        else
        {
            await db.SaveChangesAsync();
        }

        return await GetContractByIdAsync(id);
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Maps a Contract entity (with Patient + Payments + Package navigations loaded)
    /// to its list DTO. Uses <see cref="FinanceMappers.NormalizeCurrency"/> for the
    /// currency code. Moved from FinanceService as a private static helper (only
    /// used by GetContractsAsync).
    /// </summary>
    private static ContractListDto MapContractList(Contract c) => new()
    {
        Id = c.Id,
        PatientId = c.PatientId,
        // See the same null-guard note in GetContractByIdAsync above — Patient can be
        // null here for a soft-deleted patient even though the FK is required.
        PatientName = c.Patient != null ? $"{c.Patient.FirstName} {c.Patient.LastName}" : "مريض محذوف",
        PatientNumber = c.Patient?.PatientNumber ?? "—",
        Specialty = c.Specialty,
        Currency = FinanceMappers.NormalizeCurrency(c.Currency),
        TotalAmount = c.TotalAmount,
        DownPayment = c.DownPayment,
        PaidAmount = c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
        RemainingAmount = c.TotalAmount - c.DiscountAmount - c.Payments.Where(p => p.IsActive).Sum(p => p.AppliedAmount == 0 ? p.Amount : p.AppliedAmount),
        InstallmentsCount = c.InstallmentsCount,
        InstallmentAmount = c.InstallmentAmount,
        StartDate = c.StartDate?.ToString("yyyy-MM-dd"),
        Status = c.Status.ToString(),
        // YOLO-S2: package link (display-only — pricing still driven by TotalAmount)
        PackageId = c.PackageId,
        PackageName = c.Package?.Name,
        PackageColor = c.Package?.Color,
    };
}
