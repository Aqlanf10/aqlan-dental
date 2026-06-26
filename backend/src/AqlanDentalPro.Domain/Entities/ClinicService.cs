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

    /// <summary>
    /// YOLO-S2: Optional hex color (e.g. "#3b82f6") used to tag this service on the
    /// calendar and clinic queue display. Nullable so existing services (which have
    /// no color) keep the prior behavior — the UI falls back to the category color.
    /// Mirrors TreatmentPackage.Color / Appointment.AppointmentColor (varchar(20)).
    /// </summary>
    public string? Color { get; set; }

    // ── YOLO-S2: Catalog bindings (consumables + package links) ─────────────────
    /// <summary>Materials consumed per session of this service (ServiceConsumable link table).</summary>
    public ICollection<ServiceConsumable> Consumables { get; set; } = [];
    /// <summary>Reverse navigation: packages that include this service (TreatmentPackageService link).</summary>
    public ICollection<TreatmentPackageService> PackageLinks { get; set; } = [];

    // ── Commission default settings ──────────────────────────────────────────
    public decimal DefaultMaterialCost { get; set; } = 0;
    public MaterialCostType DefaultMaterialCostType { get; set; } = MaterialCostType.FixedAmount;
    public decimal DefaultLabCost { get; set; } = 0;
    /// <summary>Overrides Doctor.DefaultCommissionPercentage when set.</summary>
    public decimal? DefaultDoctorCommissionPercentage { get; set; }
    public CommissionBaseRule CommissionBaseRule { get; set; } = CommissionBaseRule.AfterDiscountAndCosts;
    public CommissionRecognitionMode CommissionRecognitionMode { get; set; } = CommissionRecognitionMode.OnPaymentCollection;
}
