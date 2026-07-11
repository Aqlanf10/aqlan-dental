using System.Text.Json;

namespace AqlanDentalPro.Application.DTOs.Ceph;

/// <summary>
/// Converts the overloaded CephAnalysis.Notes storage value into the clinical
/// note exposed by API DTOs. After calibration, the database column contains a
/// JSON envelope with calibration fields plus UserNotes; API consumers must not
/// receive that internal envelope as if it were clinical text.
/// </summary>
internal static class CephClinicalNoteParser
{
    public static string? Extract(string? storedValue)
    {
        var trimmed = storedValue?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)) return null;

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return trimmed;

            var root = document.RootElement;
            var isCalibrationEnvelope =
                root.TryGetProperty("PixelsPerMm", out _) ||
                root.TryGetProperty("pixelsPerMm", out _) ||
                root.TryGetProperty("ImageWidth", out _) ||
                root.TryGetProperty("imageWidth", out _) ||
                root.TryGetProperty("ImageHeight", out _) ||
                root.TryGetProperty("imageHeight", out _) ||
                root.TryGetProperty("UserNotes", out _) ||
                root.TryGetProperty("userNotes", out _);

            if (!isCalibrationEnvelope) return trimmed;

            if (!root.TryGetProperty("UserNotes", out var userNotes) &&
                !root.TryGetProperty("userNotes", out userNotes))
                return null;

            if (userNotes.ValueKind != JsonValueKind.String) return null;
            var value = userNotes.GetString()?.Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch (JsonException)
        {
            // Legacy plain text and malformed JSON-looking notes remain visible;
            // silently discarding a doctor's note would be worse than preserving it.
            return trimmed;
        }
    }
}

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
    public string? Reasoning { get; set; }
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
    private string? _notes;

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

    /// <summary>
    /// Clinical user note only. Calibration metadata stored in the entity's
    /// Notes column is intentionally removed at the DTO boundary.
    /// </summary>
    public string? Notes
    {
        get => _notes;
        set => _notes = CephClinicalNoteParser.Extract(value);
    }

    public double? PixelsPerMm { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public List<CephLandmarkDto> Landmarks { get; set; } = [];
    public List<CephMeasurementDto> Measurements { get; set; } = [];
    public CephDiagnosisDto? Diagnosis { get; set; }
    public bool IsApproved { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public string? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }
}

public class ApproveCephAnalysisRequest
{
    public string? Notes { get; set; }
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
    public string? Reasoning { get; set; }
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
    public string? Precision { get; set; }
}

public class CephAiTraceResultDto
{
    public List<CephLandmarkDto> Landmarks { get; set; } = [];
    public string ModelId { get; set; } = string.Empty;
    public string Disclaimer { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

public class CephAiRefineLandmarkRequest
{
    public string LandmarkKey { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public double CurrentX { get; set; }
    public double CurrentY { get; set; }
}

public class CephAiRefineResultDto
{
    public CephLandmarkDto? Landmark { get; set; }
    public string ModelId { get; set; } = string.Empty;
    public string Disclaimer { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

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
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public string? Sex { get; set; }
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
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public string? Sex { get; set; }
}

public class CreateCephNormRequest
{
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
    public int? AgeMin { get; set; }
    public int? AgeMax { get; set; }
    public string? Sex { get; set; }
}

public class CephNormBestMatchRequest
{
    public string MeasurementName { get; set; } = string.Empty;
    public string AnalysisGroup { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? Sex { get; set; }
}

public class CreateCephVersionRequest
{
    public string Label { get; set; } = string.Empty;
}

public class CephVersionListDto
{
    public Guid Id { get; set; }
    public Guid CephAnalysisId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SnapshotDate { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CephVersionDetailDto
{
    public Guid Id { get; set; }
    public Guid CephAnalysisId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SnapshotDate { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<CephLandmarkDto> Landmarks { get; set; } = [];
    public List<CephMeasurementDto> Measurements { get; set; } = [];
    public CephDiagnosisDto? Diagnosis { get; set; }
}
