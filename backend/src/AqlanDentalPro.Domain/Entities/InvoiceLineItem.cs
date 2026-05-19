namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A single line item on an invoice.
/// Links to the Services Catalog for pricing, with a name snapshot for history safety.
/// Can optionally reference a treatment plan step or a specific visit.
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

    // Navigation properties
    public Invoice Invoice { get; set; } = null!;
    public ClinicService? Service { get; set; }
    public PatientTreatmentPlanStep? RelatedTreatmentPlanStep { get; set; }
    public Visit? RelatedVisit { get; set; }
}
