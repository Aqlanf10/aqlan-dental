namespace AqlanDentalPro.Application.DTOs.Finance;

public class DoctorCommissionSummaryDto
{
    public Guid DoctorId { get; set; }
    public Guid BranchId { get; set; }
    public string Currency { get; set; } = "YER";
    public string DoctorName { get; set; } = string.Empty;
    public int CasesCount { get; set; }
    public decimal TotalServiceValue { get; set; }
    public decimal CommissionPercentage { get; set; }
    public decimal CommissionDue { get; set; }
    public decimal CommissionPaid { get; set; }
    public decimal CommissionRemaining { get; set; }
}
