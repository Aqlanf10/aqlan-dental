namespace AqlanDentalPro.Domain.Entities;

public class MedicalHistory : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? ChronicDiseases { get; set; }
    public string? CurrentMedications { get; set; }
    public string? DrugAllergies { get; set; }
    public bool BleedingDisorders { get; set; } = false;
    public string? IsPregnant { get; set; }
    public bool TmjProblems { get; set; } = false;
    public string? PreviousSurgeries { get; set; }
    public string? Notes { get; set; }

    public Patient Patient { get; set; } = null!;
}
