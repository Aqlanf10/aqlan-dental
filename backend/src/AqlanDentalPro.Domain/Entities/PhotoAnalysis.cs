namespace AqlanDentalPro.Domain.Entities;

/// <summary>
/// A saved facial photo analysis (profile or frontal) attached to an orthodontic
/// case. The placed soft-tissue landmarks and the computed measurements are kept
/// as JSON so the analysis can be reopened and reported. Measurements are honest
/// geometry computed on the frontend — no AI is involved.
/// </summary>
public class PhotoAnalysis : BaseEntity
{
    public Guid OrthoCaseId { get; set; }

    /// <summary>"profile" (lateral) or "frontal".</summary>
    public string ViewType { get; set; } = "profile";

    /// <summary>Relative uploads URL of the photo (e.g. "/uploads/{file}").</summary>
    public string ImageFileUrl { get; set; } = string.Empty;

    /// <summary>JSON map of placed landmarks: { "key": { "x": .., "y": .. }, ... }.</summary>
    public string? LandmarksJson { get; set; }

    /// <summary>JSON array of the computed measurements (name/value/normal/severity).</summary>
    public string? MeasurementsJson { get; set; }

    /// <summary>
    /// References <c>Doctors.Id</c> (NOT <c>Users.Id</c>) — resolved from the
    /// current user via <c>Doctors.UserId</c> when the analysis is created.
    /// </summary>
    public Guid? DoctorId { get; set; }

    public string? Notes { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
}
