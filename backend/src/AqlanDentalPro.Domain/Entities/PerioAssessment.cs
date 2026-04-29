namespace AqlanDentalPro.Domain.Entities;

public class PerioAssessment : BaseEntity
{
    public Guid PatientId { get; set; }
    public DateOnly? AssessmentDate { get; set; }
    public decimal? AvgPocketDepth { get; set; }
    public int? BleedingPoints { get; set; }
    public string? RecessionLevel { get; set; }
    public string? PerioStage { get; set; }
    public string? Recommendation { get; set; }
    public Guid? DoctorId { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
