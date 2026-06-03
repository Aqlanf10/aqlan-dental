namespace AqlanDentalPro.Application.DTOs.Finance;

/// <summary>
/// DTO for doctor commission calculated from actual payment collections.
/// Commission = (Collected - LabCost - MaterialCost - OtherDirectCosts) × DoctorPercentage
/// </summary>
public class DoctorCommissionEarnedFromCollectionsDto
{
    public Guid DoctorId { get; set; }
    public string DoctorName { get; set; } = string.Empty;
    public int CasesCount { get; set; }
    public decimal TotalServiceValue { get; set; }
    public decimal TotalCollected { get; set; }
    public decimal TotalLabCost { get; set; }
    public decimal TotalMaterialCost { get; set; }
    public decimal TotalOtherDirectCosts { get; set; }
    public decimal NetCommissionableAmount { get; set; }
    public decimal DoctorPercentage { get; set; }
    public decimal CommissionDue { get; set; }
    public decimal CommissionPaid { get; set; }
    public decimal CommissionRemaining { get; set; }
}
