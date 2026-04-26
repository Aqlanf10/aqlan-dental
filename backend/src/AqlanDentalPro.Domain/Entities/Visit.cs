using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class Visit : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? AppointmentId { get; set; }
    public DateOnly VisitDate { get; set; }
    public string? VisitType { get; set; }
    public Specialty? Specialty { get; set; }
    public Guid? DoctorId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? ClinicalNotes { get; set; }
    public string? TreatmentDone { get; set; }
    public string? Instructions { get; set; }
    public decimal? Cost { get; set; }
    public DateOnly? NextVisitDate { get; set; }

    public Patient Patient { get; set; } = null!;
    public Appointment? Appointment { get; set; }
    public Doctor? Doctor { get; set; }
    public ICollection<GeneralTreatment> GeneralTreatments { get; set; } = [];
    public ICollection<Prescription> Prescriptions { get; set; } = [];
}
