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

    // ── Clinical approval gate (Sprint 6 — CEPH-EPIC) ─────────────────────────
    // The final PDF report cannot be issued until an authorized doctor/admin
    // approves the analysis. These columns are added idempotently to existing
    // databases in StartupDatabaseMaintenance.EnsureCephApprovalColumnsAsync.
    public bool IsApproved { get; set; } = false;
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public ICollection<CephLandmark> Landmarks { get; set; } = [];
    public ICollection<CephMeasurement> Measurements { get; set; } = [];
    public CephDiagnosis? Diagnosis { get; set; }
}
