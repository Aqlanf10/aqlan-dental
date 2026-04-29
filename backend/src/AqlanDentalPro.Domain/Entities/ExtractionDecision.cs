using System.Text.Json;

namespace AqlanDentalPro.Domain.Entities;

public class ExtractionDecision : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public string? Decision { get; set; }
    public string? AiRecommendation { get; set; }
    public JsonDocument? ProExtraction { get; set; }
    public JsonDocument? ConExtraction { get; set; }
    public string? DoctorNotes { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTime? DecidedAt { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
