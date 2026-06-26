namespace AqlanDentalPro.Application.DTOs.Finance;

public class ContractListDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientNumber { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public string Currency { get; set; } = "YER";
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

public class AccountStatementDto
{
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientNumber { get; set; } = string.Empty;
    public decimal TotalContracted { get; set; }
    public decimal TotalDiscounts { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRemaining { get; set; }
    public int ActiveContracts { get; set; }
    public int CompletedContracts { get; set; }
    public List<ContractStatementDto> Contracts { get; set; } = [];
    public List<PaymentDto> RecentPayments { get; set; } = [];
}

public class ContractStatementDto
{
    public Guid Id { get; set; }
    public string? Specialty { get; set; }
    public string Currency { get; set; } = "YER";
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string? StartDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public int InstallmentsCount { get; set; }
    public decimal? InstallmentAmount { get; set; }
}

public class OverdueContractDto
{
    public Guid ContractId { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientNumber { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Specialty { get; set; }
    public string Currency { get; set; } = "YER";
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal OverdueAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public int MonthsElapsed { get; set; }
    public string? StartDate { get; set; }
}
