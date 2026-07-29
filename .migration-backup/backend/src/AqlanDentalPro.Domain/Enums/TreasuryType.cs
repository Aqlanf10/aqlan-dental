namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Type of a treasury vault or account.
/// </summary>
public enum TreasuryType
{
    /// <summary>Physical cash vault or cashier drawer.</summary>
    Vault,

    /// <summary>Bank account. Card/POS collections currently route here too.</summary>
    Bank
}
