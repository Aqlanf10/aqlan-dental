using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// The shared Ortho-Surgical (orthognathic) planning case — a BRIDGE between the
/// existing OrthoCase, CephAnalysis and SurgeryCase, NOT a replacement for any of them.
///
/// It owns only the joint workflow + dual-approval state. Diagnosis, cephalometry,
/// records and the actual operation are READ from their source entities via the
/// references below; nothing is duplicated here. See
/// docs/ortho-module/ORTHO_SURGICAL_WORKSPACE_IMPLEMENTATION_PLAN.md.
/// </summary>
public class OrthoSurgicalCase : BaseEntity
{
    public string CaseNumber { get; set; } = string.Empty;

    // ── References (the bridge centralizes these; no scattered FKs on other entities) ──
    public Guid PatientId { get; set; }
    public Guid OrthoCaseId { get; set; }
    public Guid? CephAnalysisId { get; set; }
    /// <summary>Set when the plan enters SurgeryScheduled and a SurgeryCase is created.</summary>
    public Guid? SurgeryCaseId { get; set; }

    /// <summary>Doctors.Id of the orthodontist (NOT Users.Id — convert via Doctors.UserId).</summary>
    public Guid? OrthodontistId { get; set; }
    /// <summary>Doctors.Id of the oral surgeon (NOT Users.Id — convert via Doctors.UserId).</summary>
    public Guid? SurgeonId { get; set; }
    public Guid? BranchId { get; set; }

    public OrthoSurgicalStatus Status { get; set; } = OrthoSurgicalStatus.DraftByOrthodontist;
    public string? DiagnosisSummary { get; set; }

    // ── Dual approval — the joint plan is final only when BOTH are set (enforced in Backend). ──
    public DateTime? OrthodontistApprovedAt { get; set; }
    public DateTime? SurgeonApprovedAt { get; set; }

    // Navigation (read-only bridge; inverse collections are intentionally NOT added to the
    // existing entities to avoid modifying them — relationships configured with WithMany()).
    public Patient Patient { get; set; } = null!;
    public OrthoCase OrthoCase { get; set; } = null!;
    public CephAnalysis? CephAnalysis { get; set; }
    public SurgeryCase? SurgeryCase { get; set; }
    public Doctor? Orthodontist { get; set; }
    public Doctor? Surgeon { get; set; }
    public SurgeonReview? SurgeonReview { get; set; }
    public JointPlan? JointPlan { get; set; }
    public ICollection<OrthoSurgicalComment> Comments { get; set; } = [];
}
