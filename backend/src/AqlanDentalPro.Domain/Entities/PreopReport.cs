using System.Text.Json;

namespace AqlanDentalPro.Domain.Entities;

public class PreopReport : BaseEntity
{
    public Guid SurgeryCaseId { get; set; }
    public DateOnly? SurgeryDate { get; set; }
    public string? SurgeryLocation { get; set; }
    public string? AnesthesiaType { get; set; }
    public JsonDocument? Checklist { get; set; }
    public JsonDocument? RequiredTests { get; set; }
    public bool ConsentSigned { get; set; } = false;
    public Guid? DoctorId { get; set; }

    public SurgeryCase SurgeryCase { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
