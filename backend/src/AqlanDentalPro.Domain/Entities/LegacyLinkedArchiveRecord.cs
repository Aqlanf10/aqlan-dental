namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Source-linked legacy patient data whose clinical or financial meaning is not sufficiently
/// proven for live mapping. Preserved for admin review without affecting any live module.
/// </summary>
public class LegacyLinkedArchiveRecord : BaseEntity
{
    public Guid PatientId { get; set; }
    public string SourceSystem { get; set; } = "Dent2026";
    public string SourceTable { get; set; } = string.Empty;
    public string SourceRecordId { get; set; } = string.Empty;
    public string Classification { get; set; } = "UnmappedReference";
    public string? LegacyFileNumber { get; set; }
    public int? LegacyTypeId { get; set; }
    public DateTime? DateValue01 { get; set; }
    public DateTime? DateValue02 { get; set; }
    public decimal? NumberValue01 { get; set; }
    public string? AccountName { get; set; }
    public string? Notes { get; set; }

    public Patient Patient { get; set; } = null!;
}
