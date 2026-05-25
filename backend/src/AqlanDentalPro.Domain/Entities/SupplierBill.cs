using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A supplier bill / accounts payable record.
/// Represents an invoice received from a supplier (dental materials, equipment, external lab) 
/// that may be paid immediately or in installments over time.
/// </summary>
public class SupplierBill : BaseEntity
{
    /// <summary>Sequential bill reference number (e.g., BILL-20260525-001).</summary>
    public string BillNumber { get; set; } = string.Empty;

    /// <summary>The supplier who issued this bill.</summary>
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    /// <summary>Short description of what was purchased (e.g., "مواد تعبئة كمبوزيت - دفعة مايو").</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Total invoice amount in YER as stated by the supplier.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Amount already paid towards this bill via installments.</summary>
    public decimal PaidAmount { get; set; } = 0;

    /// <summary>Remaining balance = TotalAmount - PaidAmount.</summary>
    public decimal RemainingAmount => TotalAmount - PaidAmount;

    /// <summary>Current payment status of this bill.</summary>
    public BillStatus Status { get; set; } = BillStatus.Unpaid;

    /// <summary>Date the bill was issued by the supplier.</summary>
    public DateOnly BillDate { get; set; }

    /// <summary>Optional due date for full payment (for credit terms).</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Optional link to a purchase order that generated this bill.</summary>
    public Guid? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    /// <summary>Optional link to an external lab order this bill pays for.</summary>
    public Guid? LabOrderId { get; set; }
    public LabOrder? LabOrder { get; set; }

    /// <summary>Scanned invoice image or PDF URL.</summary>
    public string? AttachmentUrl { get; set; }

    public string? Notes { get; set; }

    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }

    public Guid CreatedBy { get; set; }

    // Navigation
    public ICollection<SupplierBillPayment> Payments { get; set; } = [];
}
