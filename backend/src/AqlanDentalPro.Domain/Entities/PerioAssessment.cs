using System.Text.Json;

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

    // Detailed periodontal data
    public JsonDocument? PocketDepths { get; set; }
    public JsonDocument? BleedingOnProbing { get; set; }
    public JsonDocument? Mobility { get; set; }
    public JsonDocument? FurcationInvolvement { get; set; }
    public decimal? PlaqueIndex { get; set; }
    public decimal? GingivalIndex { get; set; }
    public decimal? AttachmentLoss { get; set; }
    public string? BoneLossPattern { get; set; }
    public string? TreatmentPlan { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
