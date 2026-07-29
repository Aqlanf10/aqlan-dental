namespace AqlanDentalPro.Domain.Entities;

public class DentalHistory : BaseEntity
{
    public Guid PatientId { get; set; }
    public string? ChiefComplaint { get; set; }
    public string? PreviousTreatments { get; set; }
    public bool MouthBreathing { get; set; } = false;
    public bool Bruxism { get; set; } = false;
    public bool ThumbSucking { get; set; } = false;
    public bool TongueThrusing { get; set; } = false;
    public string? Notes { get; set; }

    public Patient Patient { get; set; } = null!;
}
