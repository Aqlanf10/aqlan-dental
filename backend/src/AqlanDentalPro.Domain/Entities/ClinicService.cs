using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class ClinicService : BaseEntity
{
    public string ArabicName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Department { get; set; }
    public ServiceCategory Category { get; set; } = ServiceCategory.Other;
    public string? Description { get; set; }
    public int DefaultDurationMinutes { get; set; } = 30;
    public decimal DefaultPrice { get; set; } = 0;
    public bool RequiresDoctor { get; set; } = true;
    public bool RequiresConsultationFee { get; set; } = false;
    public bool ShowInBooking { get; set; } = true;
    public bool ShowInReception { get; set; } = true;
    public bool ShowInTreatmentPlan { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // ── Commission default settings ──────────────────────────────────────────
    public decimal DefaultMaterialCost { get; set; } = 0;
    public MaterialCostType DefaultMaterialCostType { get; set; } = MaterialCostType.FixedAmount;
    public decimal DefaultLabCost { get; set; } = 0;
    /// <summary>Overrides Doctor.DefaultCommissionPercentage when set.</summary>
    public decimal? DefaultDoctorCommissionPercentage { get; set; }
    public CommissionBaseRule CommissionBaseRule { get; set; } = CommissionBaseRule.AfterDiscountAndCosts;
    public CommissionRecognitionMode CommissionRecognitionMode { get; set; } = CommissionRecognitionMode.OnPaymentCollection;
}
