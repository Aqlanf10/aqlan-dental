namespace AqlanDentalPro.Application.DTOs.Finance;

public class CreateContractRequest
{
    public Guid PatientId { get; set; }
    public string? Specialty { get; set; }
    public Guid? RelatedCaseId { get; set; }
    public string? Currency { get; set; } = "YER";
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; } = 0;
    public string? DownPaymentMethod { get; set; } = "cash"; // Sprint Patient-Finance-Ledger: was hardcoded "cash"
    public int InstallmentsCount { get; set; } = 1;
    public decimal? InstallmentAmount { get; set; }
    public string? StartDate { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public string? DiscountReason { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// YOLO-S2: Optional link to a TreatmentPackage (e.g. "باقة تبييض كاملة") that this
    /// contract fulfills. Null = standalone contract. Pricing is still driven by
    /// TotalAmount; the package is catalog metadata for display + calendar tagging.
    /// </summary>
    public Guid? PackageId { get; set; }
}
