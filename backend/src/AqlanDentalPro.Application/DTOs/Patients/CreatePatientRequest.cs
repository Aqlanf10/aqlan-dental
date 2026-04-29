namespace AqlanDentalPro.Application.DTOs.Patients;

public class CreatePatientRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Address { get; set; }
    public string? Occupation { get; set; }
    public string? ReferralSource { get; set; }
    public Guid? PrimaryDoctorId { get; set; }
    public MedicalHistoryDto? MedicalHistory { get; set; }
    public DentalHistoryDto? DentalHistory { get; set; }
}

public class UpdatePatientRequest : CreatePatientRequest { }
