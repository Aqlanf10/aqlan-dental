using System.Text.Json;

namespace AqlanDentalPro.Domain.Entities;

public class Prescription : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? VisitId { get; set; }
    public Guid? DoctorId { get; set; }
    public string? Diagnosis { get; set; }
    public JsonDocument Drugs { get; set; } = JsonDocument.Parse("[]");
    public string? Notes { get; set; }

    public Patient Patient { get; set; } = null!;
    public Visit? Visit { get; set; }
    public Doctor? Doctor { get; set; }
}
