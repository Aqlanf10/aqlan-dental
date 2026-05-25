using AqlanDentalPro.Domain.Enums;
using AqlanDentalPro.Domain.Interfaces;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Represents a physical cash vault or a bank account for Dr. Aqlan Dental Center.
/// Tracks current liquidity balances in YER (Yemeni Rials).
/// </summary>
public class Treasury : BaseEntity, ISoftDeletable
{
    /// <summary>The name of the treasury (e.g., 'الخزنة الحديدية للمركز', 'حساب بنك التضامن').</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type of the treasury: Vault or Bank.</summary>
    public TreasuryType Type { get; set; }

    /// <summary>Current balance in Yemeni Riyals (YER).</summary>
    public decimal Balance { get; set; }

    /// <summary>Branch ID where the treasury belongs.</summary>
    public Guid BranchId { get; set; }
    public Branch? Branch { get; set; }
}
