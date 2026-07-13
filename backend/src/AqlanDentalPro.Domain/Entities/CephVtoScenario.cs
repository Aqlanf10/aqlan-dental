namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// Immutable doctor-authored visual treatment objective. Editing creates a new
/// version in the same ScenarioGroupId; no biological or soft-tissue response
/// is predicted.
/// </summary>
public class CephVtoScenario : BaseEntity
{
    public Guid CephAnalysisId { get; set; }
    public Guid ScenarioGroupId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string Name { get; set; } = string.Empty;
    public decimal UpperIncisorMoveMm { get; set; }
    public decimal LowerIncisorMoveMm { get; set; }
    public decimal? OverjetBeforeMm { get; set; }
    public decimal? OverjetAfterMm { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }

    public CephAnalysis Analysis { get; set; } = null!;
}
