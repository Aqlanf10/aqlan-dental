namespace AqlanDentalPro.Domain.Entities;

public class OrthoClinicalPhoto : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string PhotoType { get; set; } = "Intraoral"; // Intraoral, Extraoral, Progress, Radiograph
    public string? Caption { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; } = 0;

    public OrthoCase OrthoCase { get; set; } = null!;
}
