using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A single line item on an invoice.
/// Links to the Services Catalog for pricing, with a name snapshot for history safety.
/// Can optionally reference a treatment plan step or a specific visit.
/// Commission fields track the net commissionable amount and doctor share per line.
/// </summary>
public class InvoiceLineItem : BaseEntity
{
    public Guid InvoiceId { get; set; }

    /// <summary>Linked service from the catalog (optional).</summary>
    public Guid? ServiceId { get; set; }

    /// <summary>Snapshot of the service Arabic name at time of creation.</summary>
    public string ServiceNameSnapshot { get; set; } = string.Empty;

    /// <summary>Description of the line item (may differ from service name).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Quantity of the service/item.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Unit price at time of invoicing.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Total price = Quantity * UnitPrice.</summary>
    public decimal TotalPrice { get; set; }

    /// <summary>Optional link to a treatment plan step.</summary>
    public Guid? RelatedTreatmentPlanStepId { get; set; }

    /// <summary>Optional link to a specific visit (different from invoice-level visit).</summary>
    public Guid? RelatedVisitId { get; set; }

    /// <summary>Sort order for display.</summary>
    public int SortOrder { get; set; }

    // ── Commission fields ────────────────────────────────────────────────────

    /// <summary>Doctor who performed this specific line item (may differ from invoice doctor).</summary>
    public Guid? DoctorId { get; set; }

    /// <summary>Discount applied to this line item specifically.</summary>
    public decimal LineDiscountAmount { get; set; } = 0;

    /// <summary>Material/supply cost deducted before commission calculation.</summary>
    public decimal MaterialCost { get; set; } = 0;

    /// <summary>Lab fee deducted before commission calculation.</summary>
    public decimal LabCost { get; set; } = 0;

    /// <summary>Any other direct cost deducted before commission calculation.</summary>
    public decimal OtherDirectCost { get; set; } = 0;

    /// <summary>Which rule was used to compute the commissionable base.</summary>
    public CommissionBaseRule CommissionBaseRule { get; set; } = CommissionBaseRule.AfterDiscountAndCosts;

    /// <summary>Doctor commission percentage (0-100).</summary>
    public decimal DoctorCommissionPercentage { get; set; } = 0;

    /// <summary>
    /// Computed: base − materialCost − labCost − otherDirectCost.
    /// Stored for report queries (recalculated on every save).
    /// </summary>
    public decimal NetCommissionableAmount { get; set; } = 0;

    /// <summary>Computed: NetCommissionableAmount × DoctorCommissionPercentage / 100.</summary>
    public decimal DoctorCommissionAmount { get; set; } = 0;

    /// <summary>Computed: NetCommissionableAmount − DoctorCommissionAmount.</summary>
    public decimal CenterShareAmount { get; set; } = 0;

    public CommissionStatus CommissionStatus { get; set; } = CommissionStatus.Pending;

    public string? CommissionNotes { get; set; }

    /// <summary>Lab order linked to this line item (lab fee pulled from actual lab order).</summary>
    public Guid? LabOrderId { get; set; }

    /// <summary>User who approved the commission (locks editing).</summary>
    public Guid? CommissionApprovedBy { get; set; }

    public DateTime? CommissionApprovedAt { get; set; }

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;
    public ClinicService? Service { get; set; }
    public Doctor? Doctor { get; set; }
    public LabOrder? LabOrder { get; set; }
    public PatientTreatmentPlanStep? RelatedTreatmentPlanStep { get; set; }
    public Visit? RelatedVisit { get; set; }
}
