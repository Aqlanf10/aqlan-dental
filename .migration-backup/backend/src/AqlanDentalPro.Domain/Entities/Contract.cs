using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class Contract : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? Specialty { get; set; }
    public Guid? RelatedCaseId { get; set; }
    public string Currency { get; set; } = "YER";
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; } = 0;
    public int InstallmentsCount { get; set; } = 1;
    public decimal? InstallmentAmount { get; set; }
    public DateOnly? StartDate { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public string? DiscountReason { get; set; }
    /// <summary>M2 FIX: Changed from string to ContractStatus enum with HasConversion&lt;string&gt; for DB compatibility.</summary>
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public string? Notes { get; set; }
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// YOLO-S2: Optional link to a TreatmentPackage (e.g. "باقة تبييض كاملة") that this
    /// contract fulfills. Nullable + ON DELETE SET NULL so deleting a package never
    /// silently drops historical contract references. The package is catalog metadata
    /// only — pricing on the contract is still driven by TotalAmount. Reception can
    /// use this to pre-fill the package's color/name on the calendar.
    /// </summary>
    public Guid? PackageId { get; set; }
    public TreatmentPackage? Package { get; set; }

    public Patient Patient { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = [];
}
