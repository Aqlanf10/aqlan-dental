namespace AqlanDentalPro.Application.DTOs.Ceph;

public class CephAnalysisListDto
{
    public Guid Id { get; set; }
    public Guid OrthoCaseId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
    public string AnalysisDate { get; set; } = string.Empty;
    public string? XrayFileUrl { get; set; }
    public bool AiAssisted { get; set; }
    public int LandmarkCount { get; set; }
    public bool HasMeasurements { get; set; }
    public string? Notes { get; set; }
    /// <summary>
    /// Creation timestamp — the deterministic tiebreaker the presentation deck
    /// generator uses (AnalysisDate DESC, then CreatedAt DESC) to pick the
    /// "latest" analysis. Exposed so the UI selects the same record.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

public class CephLandmarkDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsAiPlaced { get; set; }
    public double? Confidence { get; set; }
}

public class CephMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public double? Value { get; set; }
    public double? Normal { get; set; }
    public double? StdDev { get; set; }
    public string? Unit { get; set; }
    public double? Deviation { get; set; }
    public string Severity { get; set; } = "normal";
    public string Direction { get; set; } = "within";
    public string AnalysisGroup { get; set; } = "steiner";
    public string? InterpretationAr { get; set; }
}

public class CephDiagnosisDto
{
    public string? SkeletalClass { get; set; }
    public string? VerticalPattern { get; set; }
    public string? IncisorInclination { get; set; }
    public string? SoftTissueSummary { get; set; }
    public string? AiRecommendation { get; set; }
    public bool DoctorApproved { get; set; }
    public string? FinalDiagnosis { get; set; }
}

public class CephAnalysisDetailDto
{
    public Guid Id { get; set; }
    public Guid OrthoCaseId { get; set; }
    public Guid PatientId { get; set; }
    public string CaseNumber { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
    public string AnalysisDate { get; set; } = string.Empty;
    public string? XrayFileUrl { get; set; }
    public bool IsAutoTraced { get; set; }
    public bool AiAssisted { get; set; }
    public Guid? DoctorId { get; set; }
    public string? Notes { get; set; }
    public double? PixelsPerMm { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public List<CephLandmarkDto> Landmarks { get; set; } = [];
    public List<CephMeasurementDto> Measurements { get; set; } = [];
    public CephDiagnosisDto? Diagnosis { get; set; }
}

public class CreateCephAnalysisRequest
{
    public Guid OrthoCaseId { get; set; }
    public string AnalysisType { get; set; } = "steiner";
    public string? XrayFileUrl { get; set; }
    public bool AiAssisted { get; set; }
    public string? Notes { get; set; }
}

public class SaveLandmarksRequest
{
    public List<LandmarkInput> Landmarks { get; set; } = [];
    public double PixelsPerMm { get; set; } = 1.0;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}

public class LandmarkInput
{
    public string Key { get; set; } = string.Empty;
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsAiPlaced { get; set; }
    public double? Confidence { get; set; }
}

public class SaveDiagnosisRequest
{
    public string? SkeletalClass { get; set; }
    public string? VerticalPattern { get; set; }
    public string? IncisorInclination { get; set; }
    public string? SoftTissueSummary { get; set; }
    public string? FinalDiagnosis { get; set; }
    public bool DoctorApproved { get; set; }
}

public class AiSimulateRequest
{
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public double PixelsPerMm { get; set; } = 1.0;
}

/// <summary>
/// Result of the template-based landmark simulation. Explicitly labeled as a
/// simulation (NOT AI) — the notice must always reach the client.
/// </summary>
public class CephSimulationResultDto
{
    public bool IsSimulation { get; set; } = true;
    public string SimulationNotice { get; set; } = string.Empty;
    public List<CephLandmarkDto> Landmarks { get; set; } = [];
}

public class CephAiTraceRequest
{
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
}

public class CephAiTraceResultDto
{
    public List<CephLandmarkDto> Landmarks { get; set; } = [];
    public string ModelId { get; set; } = string.Empty;
    public string Disclaimer { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

/// <summary>C-B: pre/post comparison between two analyses of the same case.</summary>
public class CephCompareResultDto
{
    public CephCompareSideDto Base { get; set; } = new();
    public CephCompareSideDto Target { get; set; } = new();
    public string? PatientName { get; set; }
    public List<CephCompareRowDto> Rows { get; set; } = [];
}

public class CephCompareSideDto
{
    public Guid Id { get; set; }
    public string AnalysisDate { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = string.Empty;
}

public class CephCompareRowDto
{
    public string MeasurementName { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string? AnalysisGroup { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? BaseValue { get; set; }
    public decimal? TargetValue { get; set; }
    public decimal? Delta { get; set; }
    public decimal? NormalValue { get; set; }
    public decimal? StdDeviation { get; set; }
    public string? BaseClassification { get; set; }
    public string? TargetClassification { get; set; }
    /// <summary>true = target closer to normal than base; null when not computable.</summary>
    public bool? Improved { get; set; }
}

public class CephNormDto
{
    public Guid Id { get; set; }
    public string MeasurementName { get; set; } = string.Empty;
    public string? NameAr { get; set; }
    public string AnalysisGroup { get; set; } = string.Empty;
    public decimal NormalValue { get; set; }
    public decimal StdDeviation { get; set; }
    public decimal? MinNormal { get; set; }
    public decimal? MaxNormal { get; set; }
    public string Unit { get; set; } = "°";
    public string? Category { get; set; }
    public string? InterpretationBelow { get; set; }
    public string? InterpretationNormal { get; set; }
    public string? InterpretationAbove { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateCephNormRequest
{
    public decimal NormalValue { get; set; }
    public decimal StdDeviation { get; set; }
    public decimal? MinNormal { get; set; }
    public decimal? MaxNormal { get; set; }
    public string? InterpretationBelow { get; set; }
    public string? InterpretationNormal { get; set; }
    public string? InterpretationAbove { get; set; }
}
