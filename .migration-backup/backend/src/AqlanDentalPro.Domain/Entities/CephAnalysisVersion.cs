namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// CEPH-EPIC batch C-B — a named snapshot of a cephalometric analysis at a
/// point in time (e.g. "Pre-treatment", "6 months", "12 months").
///
/// The orthodontist saves a snapshot to track progress over time and compare
/// it against later analyses. The snapshot is IMMUTABLE: it stores the
/// landmarks, measurements, and diagnosis as JSON, decoupled from the live
/// CephLandmarks/CephMeasurements/CephDiagnosis rows. If the live rows are
/// later edited or soft-deleted, the snapshot keeps its original values so the
/// historical record stays honest (matches the WebCeph target: a longitudinal
/// record of the case).
///
/// Cascade delete: when a CephAnalysis is hard-deleted (rare; soft-delete is
/// the norm), its snapshots are removed too — a snapshot without its parent
/// analysis is meaningless. The FK is nullable on the EF side only because
/// snapshot creation goes through CephService.SaveVersionAsync which always
/// sets CephAnalysisId; the column itself is NOT NULL.
/// </summary>
public class CephAnalysisVersion : BaseEntity
{
    /// <summary>The cephalometric analysis this snapshot belongs to.</summary>
    public Guid CephAnalysisId { get; set; }

    /// <summary>
    /// Free-text label set by the orthodontist, e.g. "قبل العلاج",
    /// "بعد 6 أشهر". Required, max 100 chars.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of landmark snapshots: [{ "key":"S", "x":.., "y":.., "nameAr":".." }, ...].
    /// Captured at save time so editing live landmarks does not change history.
    /// </summary>
    public string LandmarksJson { get; set; } = "[]";

    /// <summary>
    /// JSON array of measurement snapshots: [{ "name":"SNA", "value":.., "normal":.., "severity":".." }, ...].
    /// Captured at save time so recomputation does not rewrite history.
    /// </summary>
    public string MeasurementsJson { get; set; } = "[]";

    /// <summary>
    /// JSON object of the diagnosis snapshot (nullable when no diagnosis was
    /// set yet at snapshot time): { "skeletalClass":"..", "finalDiagnosis":"..", ... }.
    /// </summary>
    public string? DiagnosisJson { get; set; }

    /// <summary>
    /// Calendar day the snapshot was taken (UTC+3 clinic day, via
    /// ClinicTimeProvider.ClinicToday). Used for ordering and display.
    /// </summary>
    public DateOnly SnapshotDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// The user who created the snapshot (current user id at save time).
    /// Nullable for traceability when the system creates one automatically.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    public CephAnalysis Analysis { get; set; } = null!;
}
