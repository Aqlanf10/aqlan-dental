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

    /// <summary>YOLO-S2: Optional link to a TreatmentPackage. Null = standalone contract.</summary>
    public Guid? PackageId { get; set; }
    /// <summary>YOLO-S2: Snapshot of the package name (read-only display convenience).</summary>
    public string? PackageName { get; set; }
    /// <summary>YOLO-S2: Snapshot of the package color (for calendar/queue display).</summary>
    public string? PackageColor { get; set; }
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
    /// <summary>QA-596: performed sessions with AmountDueReference but no linked invoice. Included in TotalRemaining.</summary>
    public decimal UnbilledVisitsAmount { get; set; }
    public int ActiveContracts { get; set; }
    public int CompletedContracts { get; set; }
    public List<ContractStatementDto> Contracts { get; set; } = [];
    public int TotalPaymentsCount { get; set; }
    public List<PaymentDto> Payments { get; set; } = [];
    // Backward-compatible 20-item window for older clients.
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
