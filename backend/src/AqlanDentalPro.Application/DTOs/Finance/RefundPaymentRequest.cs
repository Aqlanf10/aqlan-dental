namespace AqlanDentalPro.Application.DTOs.Finance;

public class RefundPaymentRequest
{
    public string? Reason { get; init; }

    /// <summary>Optional partial refund amount. If null or >= original amount, full refund is processed.</summary>
    public decimal? PartialAmount { get; init; }
}
