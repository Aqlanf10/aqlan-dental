namespace AqlanDentalPro.Domain.Entities;

public class RetentionVisit : BaseEntity
{
    public Guid RetentionRecordId { get; set; }
    public DateOnly? VisitDate { get; set; }
    public string? Period { get; set; }
    public string? ToothStability { get; set; }
    public string? RetainerStatus { get; set; }
    public string? Notes { get; set; }

    public RetentionRecord RetentionRecord { get; set; } = null!;
}
