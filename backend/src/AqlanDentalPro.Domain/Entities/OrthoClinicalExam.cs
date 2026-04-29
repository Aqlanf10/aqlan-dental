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

    public OrthoCase OrthoCase { get; set; } = null!;
    public Doctor? Doctor { get; set; }
}
