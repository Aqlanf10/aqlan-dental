namespace AqlanDentalPro.Domain.Entities;

public class CephAnalysis : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public DateOnly AnalysisDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string AnalysisType { get; set; } = string.Empty;
    public string? XrayFileUrl { get; set; }
    public bool IsAutoTraced { get; set; } = false;
    public bool AiAssisted { get; set; } = false;
    public Guid? DoctorId { get; set; }
    public string? Notes { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public ICollection<CephLandmark> Landmarks { get; set; } = [];
    public ICollection<CephMeasurement> Measurements { get; set; } = [];
    public CephDiagnosis? Diagnosis { get; set; }
}
