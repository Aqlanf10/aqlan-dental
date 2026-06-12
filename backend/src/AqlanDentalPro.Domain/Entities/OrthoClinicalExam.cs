namespace AqlanDentalPro.Domain.Entities;

public class OrthoClinicalExam : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public DateOnly ExamDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    // Extraoral
    public string? FacialSymmetry { get; set; }
    public string? Profile { get; set; }
    public bool? LipsCompetence { get; set; }
    public string? SmileLine { get; set; }
    public string? VerticalProportion { get; set; }
    // Intraoral
    public string? MolarRelation { get; set; }
    public string? CanineRelation { get; set; }
    public decimal? Overjet { get; set; }
    public decimal? Overbite { get; set; }
    public bool Crossbite { get; set; } = false;
    public bool OpenBite { get; set; } = false;
    public string? UpperCrowding { get; set; }
    public string? LowerCrowding { get; set; }
    public decimal? UpperSpacing { get; set; }
    public string? MidlineUpper { get; set; }
    public string? MidlineLower { get; set; }
    // Functional
    public bool? CoCrDiscrepancy { get; set; }
    public string? TmjFindings { get; set; }
    public string? Habits { get; set; }
    public string? Notes { get; set; }
    public Guid? DoctorId { get; set; }

    // ── Phase 3 — structured clinical examination (all nullable / additive) ──

    // Occlusal: right/left split + missing measures
    /// <summary>ClassI / ClassII / ClassIII</summary>
    public string? MolarRelationRight { get; set; }
    /// <summary>ClassI / ClassII / ClassIII</summary>
    public string? MolarRelationLeft { get; set; }
    /// <summary>ClassI / ClassII / ClassIII</summary>
    public string? CanineRelationRight { get; set; }
    /// <summary>ClassI / ClassII / ClassIII</summary>
    public string? CanineRelationLeft { get; set; }
    /// <summary>ClassI / ClassIIDiv1 / ClassIIDiv2 / ClassIII</summary>
    public string? IncisorRelation { get; set; }
    public decimal? OverbitePercent { get; set; }
    public bool? DeepBite { get; set; }
    /// <summary>Anterior / PosteriorUnilateralRight / PosteriorUnilateralLeft / PosteriorBilateral</summary>
    public string? CrossbiteType { get; set; }
    public bool? ScissorBite { get; set; }
    /// <summary>Signed mm: positive = right shift</summary>
    public decimal? MidlineUpperShiftMm { get; set; }
    /// <summary>Signed mm: positive = right shift</summary>
    public decimal? MidlineLowerShiftMm { get; set; }
    public decimal? UpperCrowdingMm { get; set; }
    public decimal? LowerCrowdingMm { get; set; }
    public decimal? LowerSpacingMm { get; set; }
    /// <summary>Normal / Deep / Reverse</summary>
    public string? CurveOfSpee { get; set; }
    /// <summary>Ovoid / Tapered / Square</summary>
    public string? ArchFormUpper { get; set; }
    /// <summary>Ovoid / Tapered / Square</summary>
    public string? ArchFormLower { get; set; }
    public string? BoltonDiscrepancyNote { get; set; }

    // Extraoral additions
    /// <summary>Competent / PotentiallyCompetent / Incompetent — richer than the legacy bool</summary>
    public string? LipCompetenceGrade { get; set; }
    /// <summary>Normal / Acute / Obtuse</summary>
    public string? NasolabialAngle { get; set; }
    /// <summary>Normal / Prominent / Retruded</summary>
    public string? ChinPosition { get; set; }
    public string? FunctionalShift { get; set; }
    public bool? GummySmile { get; set; }

    // Habits as structured flags (legacy Habits free-text kept)
    public bool? ThumbSucking { get; set; }
    public bool? MouthBreathing { get; set; }
    public bool? TongueThrust { get; set; }
    public bool? LipBiting { get; set; }
    public bool? NailBiting { get; set; }
    public bool? Bruxism { get; set; }

    // Intraoral health
    /// <summary>Good / Fair / Poor</summary>
    public string? OralHygiene { get; set; }
    public string? GingivalCondition { get; set; }
    public string? PeriodontalConcerns { get; set; }
    /// <summary>Comma-separated FDI tooth numbers, e.g. "11,21"</summary>
    public string? MissingTeethFdi { get; set; }
    public string? RetainedDeciduousFdi { get; set; }
    public string? ImpactedTeethFdi { get; set; }
    public string? SupernumeraryNote { get; set; }
    public string? EctopicEruptionNote { get; set; }
    public string? FrenumNote { get; set; }
    public string? TongueNote { get; set; }
    public string? CariesNote { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
