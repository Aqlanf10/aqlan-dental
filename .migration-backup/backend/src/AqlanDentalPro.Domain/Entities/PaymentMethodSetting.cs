namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Sprint 2 — Configurable payment methods for the clinic.
/// Supports multi-branch with optional BranchId.
/// IsActive is inherited from BaseEntity; HasQueryFilter is applied in configuration.
/// </summary>
public class PaymentMethodSetting : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool RequiresReferenceNumber { get; set; }
    public string? AccountName { get; set; }
    public string? AccountNumber { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
    public Guid? BranchId { get; set; }
}
