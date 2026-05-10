namespace AqlanDentalPro.Application.DTOs.Patients;

public class PatientListDto
{
    public Guid Id { get; set; }
    public string PatientNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Gender { get; set; }
    public int? Age { get; set; }
    public string? PrimaryDoctorName { get; set; }
    public string? BranchName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class PatientProfileDto
{
    public Guid Id { get; set; }
    public string PatientNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public string? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public int? Age { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? Address { get; set; }
    public string? Occupation { get; set; }
    public string? ReferralSource { get; set; }
    public Guid? PrimaryDoctorId { get; set; }
    public string? PrimaryDoctorName { get; set; }
    public Guid? BranchId { get; set; }
    public string? BranchName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public MedicalHistoryDto? MedicalHistory { get; set; }
    public DentalHistoryDto? DentalHistory { get; set; }

    // Portal account info (credentials only populated on creation, never persisted)
    public string? PortalUsername { get; set; }
    public string? PortalTemporaryPassword { get; set; }  // Only set on creation, never stored permanently
}

public class MedicalHistoryDto
{
    public string? ChronicDiseases { get; set; }
    public string? CurrentMedications { get; set; }
    public string? DrugAllergies { get; set; }
    public bool BleedingDisorders { get; set; }
    public string? IsPregnant { get; set; }
    public bool TmjProblems { get; set; }
    public string? PreviousSurgeries { get; set; }
    public string? Notes { get; set; }
}

public class DentalHistoryDto
{
    public string? ChiefComplaint { get; set; }
    public string? PreviousTreatments { get; set; }
    public bool MouthBreathing { get; set; }
    public bool Bruxism { get; set; }
    public bool ThumbSucking { get; set; }
    public bool TongueThrusing { get; set; }
    public string? Notes { get; set; }
}
