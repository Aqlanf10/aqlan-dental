namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Stores the expected and physically counted closing amounts for one currency
/// inside a cashier session. Amounts are never converted or combined across currencies.
/// </summary>
public class CashierSessionCurrencyReconciliation : BaseEntity
{
    public Guid CashierSessionId { get; set; }
    public CashierSession CashierSession { get; set; } = null!;

    public string Currency { get; set; } = "YER";

    public decimal ExpectedCash { get; set; }
    public decimal ActualCash { get; set; }
    public decimal ExpectedBank { get; set; }
    public decimal ActualBank { get; set; }

    public decimal CashVariance => ActualCash - ExpectedCash;
    public decimal BankVariance => ActualBank - ExpectedBank;
    public decimal TotalVariance => CashVariance + BankVariance;
}
