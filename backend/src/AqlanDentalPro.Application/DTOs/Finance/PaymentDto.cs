namespace AqlanDentalPro.Application.DTOs.Finance;

public class PaymentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public Guid? ContractId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentDate { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
    public string? ServiceDescription { get; set; }
    public string? Specialty { get; set; }
    public string? DoctorName { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? Notes { get; set; }
}

public class CreatePaymentRequest
{
    public Guid PatientId { get; set; }
    public Guid? ContractId { get; set; }
    public decimal Amount { get; set; }
    public string? PaymentMethod { get; set; } = "cash";
    public string? ServiceDescription { get; set; }
    public string? Specialty { get; set; }
    public Guid? DoctorId { get; set; }
    public string? Notes { get; set; }
}
