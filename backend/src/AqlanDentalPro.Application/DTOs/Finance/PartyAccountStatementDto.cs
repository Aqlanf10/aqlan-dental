namespace AqlanDentalPro.Application.DTOs.Finance;

public record PartyAccountStatementDto(
    string PartyType,
    Guid PartyId,
    string PartyName,
    IReadOnlyList<PartyCurrencyStatementDto> Currencies);

public record PartyCurrencyStatementDto(
    string Currency,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal ClosingBalance,
    string BalanceNature,
    IReadOnlyList<PartyStatementEntryDto> Entries);

public record PartyStatementEntryDto(
    DateOnly Date,
    DateTime CreatedAt,
    string DocumentNumber,
    string DocumentType,
    string Description,
    decimal Debit,
    decimal Credit,
    decimal RunningBalance,
    Guid BranchId,
    Guid SourceId,
    string Status,
    bool IsReversal);
