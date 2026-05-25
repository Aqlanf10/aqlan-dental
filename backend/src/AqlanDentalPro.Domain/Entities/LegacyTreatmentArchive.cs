namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Historical treatment line imported from the legacy desktop system.
/// These entries are archival and must not affect live invoices or balances.
/// </summary>
public class LegacyTreatmentArchive : BaseEntity
{
    public Guid PatientId { get; set; }
    public string SourceSystem { get; set; } = "Dent2026";
    public string SourceLineId { get; set; } = string.Empty;
    public string? SourceDocumentId { get; set; }
    public string? LegacyFileNumber { get; set; }
    public DateOnly? TreatmentDate { get; set; }
    public string? DocumentType { get; set; }
    public string? ServiceName { get; set; }
    public string? Description { get; set; }
    public decimal LineTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DoctorName { get; set; }
    public bool IsOrthodonticService { get; set; }

    public Patient Patient { get; set; } = null!;
}
