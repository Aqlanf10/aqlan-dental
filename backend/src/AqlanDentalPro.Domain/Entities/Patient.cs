using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class Patient : BaseEntity
{
    public string PatientNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public Gender? Gender { get; set; }
    public string? Phone { get; set; }
    public string? WhatsApp { get; set; }
    public string? NormalizedPhone { get; set; }
    public string? NormalizedWhatsApp { get; set; }
    public string? Address { get; set; }
    public string? Occupation { get; set; }
    public string? ReferralSource { get; set; }
    public Guid? PrimaryDoctorId { get; set; }
    public Guid? BranchId { get; set; }

    public Doctor? PrimaryDoctor { get; set; }
    public Branch? Branch { get; set; }
    public MedicalHistory? MedicalHistory { get; set; }
    public DentalHistory? DentalHistory { get; set; }
    public ICollection<Appointment> Appointments { get; set; } = [];
    public ICollection<Visit> Visits { get; set; } = [];
    public ICollection<OrthoCase> OrthoCases { get; set; } = [];
    public ICollection<SurgeryCase> SurgeryCases { get; set; } = [];
    public ICollection<ClinicalPhoto> Photos { get; set; } = [];
    public ICollection<Radiograph> Radiographs { get; set; } = [];
    public ICollection<Contract> Contracts { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<InternalReferral> ReferralsFrom { get; set; } = [];
    public ICollection<InternalReferral> ReferralsTo { get; set; } = [];
    public ICollection<Prescription> Prescriptions { get; set; } = [];
    public ICollection<AiRecommendation> AiRecommendations { get; set; } = [];
}
