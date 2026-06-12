namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// تصنيف صور سجلات التقويم القياسية.
/// Stored as string in OrthoClinicalPhotos.Category — this enum is used for API validation.
/// </summary>
public enum OrthoPhotoCategory
{
    Extraoral = 0,
    Intraoral = 1,
    Radiograph = 2,
    Document = 3,
}
