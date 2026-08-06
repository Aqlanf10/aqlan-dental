namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Records an actual disbursement of accumulated commission to a doctor.
/// One payment can cover multiple invoice line items.
/// </summary>
public class DoctorCommissionPayment : BaseEntity
{
    public Guid DoctorId { get; set; }
    public Guid BranchId { get; set; }
    public string Currency { get; set; } = "YER";
    /// <summary>
    /// Caller-generated retry token. It is unique inside a branch and makes a
    /// repeated HTTP request return the original disbursement instead of paying twice.
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Notes { get; set; }
    public Guid? PaidBy { get; set; }

    // Navigation
    public Doctor Doctor { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
    public ICollection<DoctorCommissionPaymentAllocation> Allocations { get; set; } = [];
}
