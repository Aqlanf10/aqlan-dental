namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// مرحلة العلاج التي التُقطت فيها الصورة (قبل/أثناء/بعد).
/// Stored as string in OrthoClinicalPhotos.TreatmentPhase — this enum is used for API validation.
/// </summary>
public enum OrthoTreatmentPhase
{
    /// <summary>قبل العلاج</summary>
    Initial = 0,
    /// <summary>أثناء العلاج</summary>
    Progress = 1,
    /// <summary>بعد العلاج</summary>
    Final = 2,
}
