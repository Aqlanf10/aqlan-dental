namespace AqlanDentalPro.Domain.Entities;

public class CephMeasurement : BaseEntity
{
    public Guid AnalysisId { get; set; }
    public string MeasurementName { get; set; } = string.Empty;
    public decimal? MeasurementValue { get; set; }
    public decimal? NormalValue { get; set; }
    public decimal? StdDeviation { get; set; }
    public string? Unit { get; set; }
    public decimal? Deviation { get; set; }
    public string? Classification { get; set; }

    public CephAnalysis Analysis { get; set; } = null!;
}
