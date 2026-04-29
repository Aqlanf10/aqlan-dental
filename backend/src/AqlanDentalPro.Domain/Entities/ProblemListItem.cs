namespace AqlanDentalPro.Domain.Entities;

public class ProblemListItem : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public int SortOrder { get; set; } = 0;

    public OrthoCase OrthoCase { get; set; } = null!;
}
