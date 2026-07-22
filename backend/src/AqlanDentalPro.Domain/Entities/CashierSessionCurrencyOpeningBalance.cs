namespace AqlanDentalPro.Domain.Entities;

public class CashierSessionCurrencyOpeningBalance : BaseEntity
{
    public Guid CashierSessionId { get; set; }
    public CashierSession CashierSession { get; set; } = null!;

    public string Currency { get; set; } = string.Empty;
    public decimal OpeningCash { get; set; }
}
