namespace AqlanDentalPro.Application.DTOs.Finance;

/// <summary>F5/H5: Request DTO for creating a standalone invoice.</summary>
public class CreateInvoiceRequest
{
    public Guid PatientId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? AppointmentId { get; set; }
    public List<CreateInvoiceLineItemRequest>? LineItems { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string? Notes { get; set; }
}

public class CreateInvoiceLineItemRequest
{
    public Guid? ServiceId { get; set; }
    public string? ServiceNameSnapshot { get; set; }
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public Guid? RelatedTreatmentPlanStepId { get; set; }
    public Guid? RelatedVisitId { get; set; }
}

public class UpdateInvoiceRequest
{
    public List<UpdateInvoiceLineItemRequest>? LineItems { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string? Notes { get; set; }
}

public class UpdateInvoiceLineItemRequest
{
    public Guid? ServiceId { get; set; }
    public string? ServiceNameSnapshot { get; set; }
    public string? Description { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public Guid? RelatedTreatmentPlanStepId { get; set; }
    public Guid? RelatedVisitId { get; set; }
}

public class CancelInvoiceRequest
{
    public string? Notes { get; set; }
}
