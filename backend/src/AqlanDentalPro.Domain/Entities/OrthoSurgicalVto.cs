namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A Surgical VTO (Visual Treatment Objective) scenario on an <see cref="OrthoSurgicalCase"/>.
///
/// Captures the planned hard-tissue jaw movement (Maxilla/Mandible/Chin in mm + rotation in
/// degrees) and the resulting predicted cephalometric measurements (SNA/SNB/ANB/Wits/Overjet)
/// derived from those movements using documented geometric relationships (see the controller's
/// <c>ComputePredictedMeasurements</c> for the source of every coefficient).
///
/// A VTO scenario is a PLANNING AID ONLY — it is never an operative decision. The mandatory
/// Arabic disclaimer "هذه محاكاة تخطيطية تقريبية ولا تُعد قرارًا جراحيًا نهائيًا." must be
/// displayed on every frontend surface that renders a VTO. <see cref="IsApprovedByOrthodontist"/>
/// is an explicit per-scenario sign-off by the orthodontist; it is NEVER set automatically on
/// creation, and it does NOT replace the case-level dual approval on
/// <see cref="OrthoSurgicalCase"/>.
///
/// Strict creation gate (enforced in the backend, see
/// <c>OrthoSurgicalCasesController.CreateVto</c>): the linked
/// <see cref="OrthoSurgicalCase.CephAnalysisId"/> must reference a
/// <see cref="CephAnalysis"/> with <see cref="CephAnalysis.IsApproved"/> = true. A 400 with an
/// Arabic message is returned otherwise — no VTO is allowed without an approved cephalometric
/// baseline, per docs/ortho-module/ORTHO_SURGICAL_A9_A11_HANDOFF.md §5.
/// </summary>
public class OrthoSurgicalVto : BaseEntity
{
    public Guid OrthoSurgicalCaseId { get; set; }

    /// <summary>
    /// Snapshot of <see cref="OrthoSurgicalCase.CephAnalysisId"/> at creation time. The
    /// FK is ON DELETE SET NULL — deleting the underlying CephAnalysis keeps the VTO history
    /// but orphans the link. We always read SNA/SNB/etc. from the linked analysis at create
    /// time and STORE the predicted deltas, so a missing baseline later does not break the
    /// stored scenario.
    /// </summary>
    public Guid? CephAnalysisId { get; set; }

    // ── Planned skeletal movement inputs (all optional — a scenario may move only one jaw) ──
    /// <summary>Signed maxillary movement in millimetres along the antero-posterior axis (+ = advancement, - = setback). Le Fort I.</summary>
    public decimal? MaxillaMoveMm { get; set; }
    /// <summary>Signed mandibular movement in millimetres (+ = advancement via BSSO/bimaxillary, - = setback).</summary>
    public decimal? MandibleMoveMm { get; set; }
    /// <summary>Signed genioplasty (chin) movement in millimetres (+ = advancement, - = reduction).</summary>
    public decimal? ChinMoveMm { get; set; }
    /// <summary>Planned occlusal-plane rotation in degrees (+ = clockwise, - = counter-clockwise) — used for open/deep bite corrections.</summary>
    public decimal? RotationDegree { get; set; }

    // ── Predicted cephalometric outcomes (computed server-side from the inputs above) ──
    // Stored as decimal? so a partial computation (e.g. missing baseline SNA) does not
    // corrupt the whole scenario — the field simply stays null and the UI shows "—".
    public decimal? PredictedSNA { get; set; }
    public decimal? PredictedSNB { get; set; }
    public decimal? PredictedANB { get; set; }
    public decimal? PredictedWits { get; set; }
    public decimal? PredictedOverjet { get; set; }

    public string? Notes { get; set; }

    /// <summary>Snapshot of the creating user's UserId (<see cref="ICurrentUserService.UserId"/>).</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Explicit orthodontist sign-off on this scenario. Defaults to <c>false</c> and is only
    /// flipped by the dedicated <c>POST .../vto/{vtoId}/approve</c> endpoint, which is
    /// restricted to Orthodontist/Admin with PatientAccessFilter — NEVER auto-set on create/update.
    /// </summary>
    public bool IsApprovedByOrthodontist { get; set; } = false;
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }

    // Navigation (read-only bridge; the inverse collection is intentionally NOT added to
    // OrthoSurgicalCase to avoid modifying it — relationship configured with WithMany()).
    public OrthoSurgicalCase OrthoSurgicalCase { get; set; } = null!;
    public CephAnalysis? CephAnalysis { get; set; }
}
