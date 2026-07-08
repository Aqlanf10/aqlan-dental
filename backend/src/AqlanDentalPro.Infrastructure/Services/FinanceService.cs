using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// TD-021 PR A4 (slices 2+3): after extracting PaymentService,
/// SupplierRefundService, and (slice 1) FinanceLedgerWriter, this service now
/// owns only the two contract methods that orchestrate payment-side calls:
/// CreateContractAsync (auto-creates the down payment via IPaymentService) and
/// UpdateContractStatusAsync (cancellation reverses linked payments). They move
/// to ContractService in the final A4 slice.
/// </summary>
public class FinanceService(AppDbContext db, ICurrentUserService currentUser, ILogger<FinanceService> logger, IJournalEntryService journalEntryService, IContractService contractService, IPaymentService paymentService)
    : IFinanceService
{
    // TD-021 PR A2: FinanceMappers.BaseCurrency + FinanceMappers.SupportedCurrencies moved to FinanceMappers.
    // GetUnbilledVisitsAmountAsync moved to FinanceReadService (read-only helper,
    // only used by read methods). FinanceMappers.MapPayment + FinanceMappers.NormalizeCurrency moved to FinanceMappers
    // (shared between FinanceService write methods and FinanceReadService read methods).

    // TD-021 PR A3: GetContractsAsync + GetContractByIdAsync moved to ContractService.
    // FinanceService injects IContractService so CreateContractAsync + UpdateContractStatusAsync
    // can call contractService.GetContractByIdAsync for their return value.

    public async Task<ContractDetailDto> CreateContractAsync(CreateContractRequest req)
    {
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

        return (await contractService.GetContractByIdAsync(contract.Id))!;
    }

    // TD-021 PR A3: UpdateContractAsync moved to ContractService.

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

        return await contractService.GetContractByIdAsync(id);
    }
}
