namespace AqlanDentalPro.Domain.Entities;

public class Setting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Category { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
