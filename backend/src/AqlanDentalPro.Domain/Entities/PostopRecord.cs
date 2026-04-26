using System.Text.Json;

namespace AqlanDentalPro.Domain.Entities;

public class PostopRecord : BaseEntity
{
    public Guid SurgeryCaseId { get; set; }
    public string? Instructions { get; set; }
    public JsonDocument? Prescription { get; set; }
    public JsonDocument? FollowupSchedule { get; set; }

    public SurgeryCase SurgeryCase { get; set; } = null!;
}
