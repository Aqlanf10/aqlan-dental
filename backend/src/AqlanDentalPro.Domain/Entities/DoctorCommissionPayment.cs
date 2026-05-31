namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Records an actual disbursement of accumulated commission to a doctor.
/// One payment can cover multiple invoice line items.
/// </summary>
public class DoctorCommissionPayment : BaseEntity
{
    public Guid DoctorId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid? PaidBy { get; set; }

    // Navigation
    public Doctor Doctor { get; set; } = null!;
}
