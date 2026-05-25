namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Historical journal amount imported from the legacy desktop system for audit and reconciliation.
/// It is deliberately separate from Payment and Contract so unverified legacy accounting does not
/// change the live patient balance.
/// </summary>
public class LegacyFinancialArchiveEntry : BaseEntity
{
    public Guid PatientId { get; set; }
    public string SourceSystem { get; set; } = "Dent2026";
    public string SourceEntryId { get; set; } = string.Empty;
    public string? LegacyFileNumber { get; set; }
    public DateOnly? EntryDate { get; set; }
    public string? AccountName { get; set; }
    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? SourceDocumentId { get; set; }
    public string ReconciliationStatus { get; set; } = "ReferenceOnly";

    public Patient Patient { get; set; } = null!;
}
