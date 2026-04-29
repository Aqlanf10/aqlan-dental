namespace AqlanDentalPro.Application.DTOs.Finance;

public class ReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime? PrintedAt { get; set; }

    // Center info for printing
    public CenterInfoDto CenterInfo { get; set; } = new();

    // Payment details
    public ReceiptPaymentInfo Payment { get; set; } = new();

    // Patient details
    public ReceiptPatientInfo Patient { get; set; } = new();

    // Contract info (if linked)
    public ReceiptContractInfo? Contract { get; set; }
}

public class CenterInfoDto
{
    public string Name { get; set; } = "مركز د. عقلان الكامل";
    public string Address { get; set; } = "الجمهورية اليمنية";
    public string Phone { get; set; } = string.Empty;
}

public class ReceiptPaymentInfo
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentDate { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? ServiceDescription { get; set; }
    public string? Specialty { get; set; }
    public string? DoctorName { get; set; }
    public string? Notes { get; set; }
}

public class ReceiptPatientInfo
{
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientNumber { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public class ReceiptContractInfo
{
    public Guid ContractId { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public int InstallmentsCount { get; set; }
    public decimal? InstallmentAmount { get; set; }
}
