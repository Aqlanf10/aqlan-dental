namespace AqlanDentalPro.Domain.Entities;

public class RetentionRecord : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public DateOnly? DebondDate { get; set; }
    public string? UpperRetainer { get; set; }
    public string? LowerRetainer { get; set; }
    public string? Instructions { get; set; }
    public string Status { get; set; } = "active";

    public OrthoCase OrthoCase { get; set; } = null!;
    public ICollection<RetentionVisit> Visits { get; set; } = [];
}
