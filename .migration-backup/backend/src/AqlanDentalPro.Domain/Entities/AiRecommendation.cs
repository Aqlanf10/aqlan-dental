using System.Text.Json;

namespace AqlanDentalPro.Domain.Entities;

public class AiRecommendation : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? Context { get; set; }
    public JsonDocument? InputData { get; set; }
    public string? Recommendation { get; set; }
    public decimal? Confidence { get; set; }
    public string? DoctorAction { get; set; } // approved, rejected, modified, pending
    public Guid? DoctorId { get; set; }
    public DateTime? ActionAt { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
