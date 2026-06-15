namespace AqlanDentalPro.Domain.Entities;

public class OrthoClinicalPhoto : BaseEntity
{
    public Guid OrthoCaseId { get; set; }
    public string PhotoUrl { get; set; } = string.Empty;
    public string PhotoType { get; set; } = "Intraoral"; // Intraoral, Extraoral, Progress, Radiograph
    public string? Caption { get; set; }
    public DateTime TakenAt { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; } = 0;
    /// <summary>Standardized record category (OrthoPhotoCategory enum name): Extraoral, Intraoral, Radiograph, Document. Nullable for legacy photos.</summary>
    public string? Category { get; set; }
    /// <summary>Standardized record subtype, e.g. FrontalRest, FrontalSmile, Profile, Frontal, Right, Left, UpperOcclusal, LowerOcclusal, OPG, LateralCeph, PACeph, CBCT.</summary>
    public string? Subtype { get; set; }
    /// <summary>Treatment phase (OrthoTreatmentPhase enum name): Initial (قبل), Progress (أثناء), Final (بعد). Nullable for legacy photos.</summary>
    public string? TreatmentPhase { get; set; }
    /// <summary>Whether this photo is selected for inclusion in the case report.</summary>
    public bool IsSelectedForReport { get; set; } = false;

    public OrthoCase OrthoCase { get; set; } = null!;
    public OrthoImagePreparation? ImagePreparation { get; set; }
}
