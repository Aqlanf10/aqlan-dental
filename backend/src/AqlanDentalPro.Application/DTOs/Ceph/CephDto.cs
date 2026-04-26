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
