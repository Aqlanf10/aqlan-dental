namespace AqlanDentalPro.Application.DTOs.Surgery;

public class UpsertOperativeReportRequest
{
    public string? SurgeryDateTime { get; set; }
    public int? DurationMinutes { get; set; }
    public string? AnesthesiaUsed { get; set; }
    public string? Technique { get; set; }
    public string? DetailedDescription { get; set; }
    public string? Outcome { get; set; }
    public string? Complications { get; set; }
    public int? SuturesCount { get; set; }
    public bool? SpecimenSent { get; set; }
    public Guid? DoctorId { get; set; }
}

public class OperativeReportDto
{
    public Guid Id { get; set; }
    public Guid SurgeryCaseId { get; set; }
    public string? SurgeryDateTime { get; set; }
    public int? DurationMinutes { get; set; }
    public string? AnesthesiaUsed { get; set; }
    public string? Technique { get; set; }
    public string? DetailedDescription { get; set; }
    public string? Outcome { get; set; }
    public string? Complications { get; set; }
    public int? SuturesCount { get; set; }
    public bool SpecimenSent { get; set; }
    public string? DoctorName { get; set; }
    public Guid? DoctorId { get; set; }
    public string? ApprovedAt { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}

public class CreateHospitalReferralRequest
{
    public string? HospitalName { get; set; }
    public string? Reason { get; set; }
    public string? ReferralDate { get; set; }
    public string? Notes { get; set; }
}

public class UpdateHospitalReferralStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class HospitalReferralDto
{
    public Guid Id { get; set; }
    public Guid SurgeryCaseId { get; set; }
    public string? HospitalName { get; set; }
    public string? Reason { get; set; }
    public string? ReferralDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
}
