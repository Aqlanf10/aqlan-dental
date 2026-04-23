namespace AqlanDentalPro.Application.DTOs.Finance;

public class ContractListDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientNumber { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public int InstallmentsCount { get; set; }
    public decimal? InstallmentAmount { get; set; }
    public string? StartDate { get; set; }
    public string Status { get; set; } = "active";
}

public class ContractDetailDto : ContractListDto
{
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public string? Notes { get; set; }
    public List<PaymentDto> Payments { get; set; } = [];
}
