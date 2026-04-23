namespace AqlanDentalPro.Application.DTOs.General;

public class GeneralTreatmentDto
{
    public Guid Id { get; set; }
    public Guid PatientId { get; set; }
    public string? PatientName { get; set; }
    public string TreatmentType { get; set; } = string.Empty;
    public string? ToothNumber { get; set; }
    public string? MaterialUsed { get; set; }
    public string? AnesthesiaType { get; set; }
    public decimal? Cost { get; set; }
    public string? DoctorName { get; set; }
    public string? Notes { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateGeneralTreatmentRequest
{
    public Guid PatientId { get; set; }
    public string TreatmentType { get; set; } = string.Empty;
    public string? ToothNumber { get; set; }
    public string? MaterialUsed { get; set; }
    public string? AnesthesiaType { get; set; }
    public decimal? Cost { get; set; }
    public Guid? DoctorId { get; set; }
    public string? Notes { get; set; }
}
