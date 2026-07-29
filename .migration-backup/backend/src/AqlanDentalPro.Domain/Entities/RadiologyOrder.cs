namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// RX-REQ-001 (spec 010): an outgoing imaging order the patient carries to an
/// external radiology center (the clinic has no OPG/CBCT machine). Distinct
/// from <see cref="Radiograph"/>, which stores the resulting image AFTER the
/// study is done. StudyType values align with Radiograph.XrayType so a later
/// "attach result" feature maps 1:1: panoramic, cbct_3d, lateral_ceph,
/// pa_ceph, bitewing, periapical, other.
/// </summary>
public class RadiologyOrder : BaseEntity
{
    public Guid PatientId { get; set; }
    public Guid? DoctorId { get; set; }
    public Guid? VisitId { get; set; }
    public string StudyType { get; set; } = "panoramic";
    /// <summary>Region of interest, e.g. "Upper right quadrant", "Teeth 14-16".</summary>
    public string? Region { get; set; }
    /// <summary>Clinical reason/notes for the radiology center (printed on the referral).</summary>
    public string? ClinicalNotes { get; set; }

    public Patient Patient { get; set; } = null!;
    public Doctor? Doctor { get; set; }
    public Visit? Visit { get; set; }
}
