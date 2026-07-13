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
    public bool IsApproved { get; set; }
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
    /// <summary>
    /// Optional short note (Arabic or English) explaining WHY the AI placed this
    /// landmark at this position. Surfaced in the canvas UI so the orthodontist
    /// can read the model's reasoning before accepting or moving the point.
    /// </summary>
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

    // ── Clinical approval gate (CEPH-EPIC) ────────────────────────────────────
    // The final PDF report is blocked until IsApproved is true.
    public bool IsApproved { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public string? ApprovedByName { get; set; }
    public string? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }
}

/// <summary>Request body for POST /api/ceph/{id}/approve — optional notes.</summary>
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
    /// <summary>
    /// Precision mode for the AI draft. <c>"draft"</c> (default) = fast first-pass;
    /// <c>"high"</c> = slower deliberate pass that cross-checks each landmark and
    /// omits anything with confidence &lt;= 0.5. Unknown / null values fall back to draft.
    /// </summary>
    public string? Precision { get; set; }
}

public class CephAiTraceResultDto
{
    public List<CephLandmarkDto> Landmarks { get; set; } = [];
    public string ModelId { get; set; } = string.Empty;
    public string Disclaimer { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
}

/// <summary>Request body for POST /api/ceph/{id}/ai/refine-landmark.</summary>
public class CephAiRefineLandmarkRequest
{
    public string LandmarkKey { get; set; } = string.Empty;
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public double CurrentX { get; set; }
    public double CurrentY { get; set; }
}

/// <summary>
/// Result of refining a single landmark. <see cref="Landmark"/> is null when the
/// model declined to refine (e.g. confidence too low) — the caller should keep
/// the current position. The result is NEVER auto-saved.
/// </summary>
public class CephAiRefineResultDto
{
    public CephLandmarkDto? Landmark { get; set; }
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

    /// <summary>CLIN-10 — lower bound (inclusive) of the patient-age band, in
    /// whole years. Null = no lower bound.</summary>
    public int? AgeMin { get; set; }

    /// <summary>CLIN-10 — upper bound (inclusive) of the patient-age band, in
    /// whole years. Null = no upper bound.</summary>
    public int? AgeMax { get; set; }

    /// <summary>CLIN-10 — "M" or "F" for sex-specific norms, null for both
    /// sexes.</summary>
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

    /// <summary>CLIN-10 — lower bound (inclusive) of the patient-age band. Null
    /// = no lower bound (the row applies to any age from 0 up to AgeMax, or to
    /// any age if AgeMax is also null).</summary>
    public int? AgeMin { get; set; }

    /// <summary>CLIN-10 — upper bound (inclusive) of the patient-age band. Null
    /// = no upper bound.</summary>
    public int? AgeMax { get; set; }

    /// <summary>CLIN-10 — "M" or "F" for a sex-specific norm, null for both
    /// sexes. When non-null, the lookup prefers this row over a sex-null row
    /// for the same age band on a matching-sex patient.</summary>
    public string? Sex { get; set; }
}

/// <summary>
/// CLIN-10 — payload for POST /api/ceph-norms. Creates a new configurable norm
/// row. Admin-only. The clinic owner (an orthodontist) uses this to add
/// population-specific norms for an age/sex stratum not covered by the factory
/// defaults.
/// </summary>
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

/// <summary>
/// CLIN-10 — query parameters for GET /api/ceph-norms/best. Returns the
/// best-matching norm for a patient's age/sex using the same priority tiers
/// as CephService.FindBestCephNorm (sex-specific+age-matched &gt; sex-null+age-
/// matched &gt; un-stratified fallback).
/// </summary>
public class CephNormBestMatchRequest
{
    public string MeasurementName { get; set; } = string.Empty;
    public string AnalysisGroup { get; set; } = string.Empty;
    public int? Age { get; set; }
    public string? Sex { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
//  CEPH-EPIC batch C-B — analysis VERSION snapshots
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Request body for POST /api/ceph/{id}/versions — save the current analysis
/// state (landmarks + measurements + diagnosis) as a named snapshot.
/// </summary>
public class CreateCephVersionRequest
{
    /// <summary>Free-text label, e.g. "قبل العلاج" / "بعد 6 أشهر". 1..100 chars.</summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>List item for GET /api/ceph/{id}/versions (no JSON blobs).</summary>
public class CephVersionListDto
{
    public Guid Id { get; set; }
    public Guid CephAnalysisId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string SnapshotDate { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Full snapshot for GET /api/ceph/{id}/versions/{versionId}. Carries the
/// deserialized landmarks/measurements/diagnosis so the compare page can load
/// a snapshot the same way it loads a live analysis.
/// </summary>
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
