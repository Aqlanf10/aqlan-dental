namespace AqlanDentalPro.Domain.Entities;

public class Receipt : BaseEntity
{
    public Guid PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime? PrintedAt { get; set; }
    public Guid? PrintedBy { get; set; }

    public Payment Payment { get; set; } = null!;
}
