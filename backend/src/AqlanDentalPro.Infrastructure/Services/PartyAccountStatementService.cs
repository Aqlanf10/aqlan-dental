using AqlanDentalPro.Application.DTOs.Finance;
using AqlanDentalPro.Application.Interfaces.Services;
using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// Canonical debit/credit statement for every external party. Journal-backed
/// parties read posted double-entry lines; doctor commissions use their
/// append-only earned/adjustment/payment records until commission accrual is
/// represented by its own journal account in a later accounting migration.
/// Currencies are never converted or combined.
/// </summary>
public sealed class PartyAccountStatementService(AppDbContext db)
    : IPartyAccountStatementService
{
    private static readonly HashSet<string> AllowedCurrencies =
        new(StringComparer.OrdinalIgnoreCase) { "YER", "SAR", "USD" };

    public async Task<PartyAccountStatementDto?> GetAsync(
        string partyType,
        Guid partyId,
        Guid? branchId,
        string? currency,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = partyType.Trim().ToLowerInvariant();
        var normalizedCurrency = NormalizeCurrency(currency);

        return normalizedType switch
        {
            "patient" => await GetJournalBackedAsync(
                "patient", partyId,
                await db.Patients.Where(x => x.Id == partyId && x.IsActive
                                          && (!branchId.HasValue || x.BranchId == branchId.Value))
                    .Select(x => (x.FirstName + " " + x.LastName).Trim())
                    .SingleOrDefaultAsync(cancellationToken),
                [partyId],
                [JournalAccountType.PatientReceivable, JournalAccountType.PatientAdvance],
                branchId, normalizedCurrency, cancellationToken),

            "supplier" => await GetSupplierAsync(
                partyId, branchId, normalizedCurrency, cancellationToken),

            "lab" => await GetLabAsync(partyId, branchId, normalizedCurrency, cancellationToken),
            "doctor" => await GetDoctorAsync(partyId, branchId, normalizedCurrency, cancellationToken),
            _ => throw new ArgumentException("نوع الحساب يجب أن يكون patient أو doctor أو supplier أو lab")
        };
    }

    private async Task<PartyAccountStatementDto?> GetLabAsync(
        Guid labId, Guid? branchId, string? currency, CancellationToken cancellationToken)
    {
        var lab = await db.Labs
            .Where(x => x.Id == labId && x.IsActive)
            .Select(x => new { x.Name, x.SupplierId, x.BranchId })
            .SingleOrDefaultAsync(cancellationToken);
        if (lab == null) return null;
        if (branchId.HasValue && lab.BranchId.HasValue && lab.BranchId != branchId)
            return null;
        Guid[] accountIds = lab.SupplierId.HasValue
            ? [lab.SupplierId.Value, labId]
            : [labId];

        return await GetJournalBackedAsync(
            "lab", labId, lab.Name, accountIds,
            [JournalAccountType.Payable, JournalAccountType.AccountsPayable],
            branchId, currency, cancellationToken);
    }

    private async Task<PartyAccountStatementDto?> GetSupplierAsync(
        Guid supplierId, Guid? branchId, string? currency, CancellationToken cancellationToken)
    {
        var supplierName = await db.Suppliers
            .Where(x => x.Id == supplierId && x.IsActive)
            .Select(x => x.Name)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(supplierName)) return null;

        // Lab settlement entries historically use LabId while the payable accrual uses
        // SupplierId. A dental-lab supplier statement therefore needs both identities.
        var accountIds = await db.Labs
            .Where(x => x.SupplierId == supplierId && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        accountIds.Insert(0, supplierId);

        return await GetJournalBackedAsync(
            "supplier", supplierId, supplierName, accountIds,
            [JournalAccountType.Payable, JournalAccountType.AccountsPayable],
            branchId, currency, cancellationToken);
    }

    private async Task<PartyAccountStatementDto?> GetJournalBackedAsync(
        string partyType,
        Guid partyId,
        string? partyName,
        IReadOnlyCollection<Guid> accountIds,
        JournalAccountType[] accountTypes,
        Guid? branchId,
        string? currency,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partyName)) return null;

        var query = db.JournalLines
            .Where(line => accountIds.Contains(line.AccountId)
                        && accountTypes.Contains(line.AccountType)
                        && line.IsActive
                        && line.JournalEntry != null
                        && line.JournalEntry.IsActive
                        && line.JournalEntry.IsPosted);
        if (branchId.HasValue) query = query.Where(line => line.BranchId == branchId.Value);
        if (currency != null) query = query.Where(line => line.JournalEntry!.Currency == currency);

        var rows = await query
            .OrderBy(line => line.JournalEntry!.EntryDate)
            .ThenBy(line => line.JournalEntry!.CreatedAt)
            .ThenBy(line => line.Id)
            .Select(line => new RawEntry(
                line.JournalEntry!.EntryDate,
                line.JournalEntry.CreatedAt,
                line.JournalEntry.EntryNumber,
                line.JournalEntry.FinancialDocumentType.ToString(),
                line.Description ?? line.JournalEntry.Description,
                line.Debit,
                line.Credit,
                line.BranchId,
                line.JournalEntry.FinancialDocumentId,
                line.JournalEntry.IsReversal ? "Reversed" : "Posted",
                line.JournalEntry.IsReversal,
                line.JournalEntry.Currency))
            .ToListAsync(cancellationToken);

        return Build(partyType, partyId, partyName, rows);
    }

    private async Task<PartyAccountStatementDto?> GetDoctorAsync(
        Guid doctorId, Guid? branchId, string? currency, CancellationToken cancellationToken)
    {
        var doctor = await db.Doctors
            .Where(x => x.Id == doctorId && x.IsActive)
            .Select(x => new { x.Name, x.BranchId })
            .SingleOrDefaultAsync(cancellationToken);
        if (doctor == null) return null;
        if (branchId.HasValue && doctor.BranchId.HasValue && doctor.BranchId != branchId)
            return null;

        var earnedQuery = db.InvoiceLineItems
            .Where(line => line.DoctorId == doctorId
                        && line.IsActive
                        && (line.CommissionStatus == CommissionStatus.Approved
                            || line.CommissionStatus == CommissionStatus.Paid));
        if (branchId.HasValue)
            earnedQuery = earnedQuery.Where(line => line.Invoice.Patient.BranchId == branchId.Value);
        if (currency != null)
            earnedQuery = earnedQuery.Where(line => line.Invoice.Currency == currency);

        var earned = await earnedQuery.Select(line => new RawEntry(
                DateOnly.FromDateTime(line.CommissionApprovedAt ?? line.CreatedAt),
                line.CommissionApprovedAt ?? line.CreatedAt,
                line.Invoice.InvoiceNumber,
                "DoctorCommission",
                "استحقاق عمولة: " + (line.ServiceNameSnapshot.Length > 0
                    ? line.ServiceNameSnapshot : line.Description),
                0m,
                line.DoctorCommissionAmount,
                line.Invoice.Patient.BranchId ?? line.Doctor!.BranchId ?? Guid.Empty,
                line.Id,
                line.CommissionStatus.ToString(),
                false,
                line.Invoice.Currency))
            .ToListAsync(cancellationToken);

        var adjustmentQuery = db.DoctorCommissionAdjustments
            .Where(x => x.DoctorId == doctorId && x.IsActive
                     && x.Status != CommissionAdjustmentStatus.Cancelled);
        if (branchId.HasValue) adjustmentQuery = adjustmentQuery.Where(x => x.BranchId == branchId.Value);
        if (currency != null) adjustmentQuery = adjustmentQuery.Where(x => x.Currency == currency);
        var adjustments = await adjustmentQuery.Select(x => new RawEntry(
                DateOnly.FromDateTime(x.CreatedAt), x.CreatedAt,
                "ADJ-" + x.Id.ToString().Substring(0, 8), "CommissionAdjustment", x.Reason,
                x.AdjustmentAmount < 0 ? Math.Abs(x.AdjustmentAmount) : 0m,
                x.AdjustmentAmount > 0 ? x.AdjustmentAmount : 0m,
                x.BranchId, x.Id, x.Status.ToString(), false, x.Currency))
            .ToListAsync(cancellationToken);

        var paymentQuery = db.DoctorCommissionPayments
            .Where(x => x.DoctorId == doctorId && x.IsActive);
        if (branchId.HasValue) paymentQuery = paymentQuery.Where(x => x.BranchId == branchId.Value);
        if (currency != null) paymentQuery = paymentQuery.Where(x => x.Currency == currency);
        var payments = await paymentQuery.Select(x => new RawEntry(
                x.PaymentDate, x.CreatedAt,
                x.ReferenceNumber ?? "COM-" + x.Id.ToString().Substring(0, 8),
                "CommissionPayment", "صرف عمولة الطبيب" +
                    (string.IsNullOrWhiteSpace(x.Notes) ? "" : ": " + x.Notes),
                x.Amount, 0m, x.BranchId, x.Id, "Posted", false, x.Currency))
            .ToListAsync(cancellationToken);

        return Build("doctor", doctorId, doctor.Name, earned.Concat(adjustments).Concat(payments));
    }

    private static PartyAccountStatementDto Build(
        string partyType, Guid partyId, string partyName, IEnumerable<RawEntry> rawRows)
    {
        var currencies = rawRows
            .GroupBy(row => row.Currency)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                decimal running = 0m;
                var entries = group
                    .OrderBy(row => row.Date)
                    .ThenBy(row => row.CreatedAt)
                    .ThenBy(row => row.SourceId)
                    .Select(row =>
                    {
                        running += row.Debit - row.Credit;
                        return new PartyStatementEntryDto(
                            row.Date, row.CreatedAt, row.DocumentNumber, row.DocumentType,
                            row.Description, row.Debit, row.Credit, running, row.BranchId,
                            row.SourceId, row.Status, row.IsReversal);
                    }).ToList();

                var debit = entries.Sum(x => x.Debit);
                var credit = entries.Sum(x => x.Credit);
                var closing = debit - credit;
                return new PartyCurrencyStatementDto(
                    group.Key, debit, credit, Math.Abs(closing),
                    closing > 0 ? "Debit" : closing < 0 ? "Credit" : "Settled", entries);
            }).ToList();

        return new PartyAccountStatementDto(partyType, partyId, partyName, currencies);
    }

    private static string? NormalizeCurrency(string? currency)
    {
        if (string.IsNullOrWhiteSpace(currency)) return null;
        var normalized = currency.Trim().ToUpperInvariant();
        return AllowedCurrencies.Contains(normalized)
            ? normalized
            : throw new ArgumentException("العملة يجب أن تكون YER أو SAR أو USD");
    }

    private sealed record RawEntry(
        DateOnly Date, DateTime CreatedAt, string DocumentNumber, string DocumentType,
        string Description, decimal Debit, decimal Credit, Guid BranchId, Guid SourceId,
        string Status, bool IsReversal, string Currency);
}
