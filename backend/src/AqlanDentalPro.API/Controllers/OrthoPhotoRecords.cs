using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Phase 2 — standardized ortho records helpers:
/// - case-insensitive validation/normalization of Category and TreatmentPhase against the enums;
/// - records-checklist auto-derivation from (Category, Subtype) tags with a backward-compatible
///   fallback to the legacy caption-keyword heuristic for untagged photos.
/// </summary>
public static class OrthoPhotoRecords
{
    // Canonical subtype names (stored as-is; matching is case-insensitive)
    public const string SubtypeFrontalRest = "FrontalRest";
    public const string SubtypeFrontalSmile = "FrontalSmile";
    public const string SubtypeProfile = "Profile";
    public const string SubtypeFrontal = "Frontal";
    public const string SubtypeRight = "Right";
    public const string SubtypeLeft = "Left";
    public const string SubtypeUpperOcclusal = "UpperOcclusal";
    public const string SubtypeLowerOcclusal = "LowerOcclusal";
    public const string SubtypeOpg = "OPG";
    public const string SubtypeLateralCeph = "LateralCeph";
    public const string SubtypePaCeph = "PACeph";
    public const string SubtypeCbct = "CBCT";

    /// <summary>
    /// Validates a category value against <see cref="OrthoPhotoCategory"/> (case-insensitive).
    /// Returns true with the canonical enum name, or true with null when input is null/blank.
    /// </summary>
    public static bool TryNormalizeCategory(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (Enum.TryParse<OrthoPhotoCategory>(value.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            normalized = parsed.ToString();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Validates a treatment phase value against <see cref="OrthoTreatmentPhase"/> (case-insensitive).
    /// Returns true with the canonical enum name, or true with null when input is null/blank.
    /// </summary>
    public static bool TryNormalizePhase(string? value, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (Enum.TryParse<OrthoTreatmentPhase>(value.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            normalized = parsed.ToString();
            return true;
        }
        return false;
    }

    private static bool Is(string? actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool HasTag(IEnumerable<OrthoClinicalPhoto> photos, OrthoPhotoCategory category, params string[] subtypes) =>
        photos.Any(p => Is(p.Category, category.ToString()) && subtypes.Any(s => Is(p.Subtype, s)));

    // Legacy caption-keyword heuristic (kept as OR'd fallback so existing untagged photos keep working)
    private static bool HasLegacy(IEnumerable<OrthoClinicalPhoto> photos, string photoType, string captionKeyword) =>
        photos.Any(p => p.PhotoType == photoType && p.Caption?.Contains(captionKeyword) == true);

    public static bool DeriveExtraoralFrontal(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Extraoral, SubtypeFrontalRest, SubtypeFrontalSmile)
        || HasLegacy(photos, "Extraoral", "frontal");

    public static bool DeriveExtraoralProfile(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Extraoral, SubtypeProfile)
        || HasLegacy(photos, "Extraoral", "profile");

    public static bool DeriveExtraoralSmile(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Extraoral, SubtypeFrontalSmile)
        || HasLegacy(photos, "Extraoral", "smile");

    public static bool DeriveIntraoralFrontal(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Intraoral, SubtypeFrontal)
        || HasLegacy(photos, "Intraoral", "frontal");

    public static bool DeriveIntraoralRight(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Intraoral, SubtypeRight)
        || HasLegacy(photos, "Intraoral", "right");

    public static bool DeriveIntraoralLeft(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Intraoral, SubtypeLeft)
        || HasLegacy(photos, "Intraoral", "left");

    public static bool DeriveUpperOcclusal(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Intraoral, SubtypeUpperOcclusal)
        || HasLegacy(photos, "Intraoral", "upper");

    public static bool DeriveLowerOcclusal(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Intraoral, SubtypeLowerOcclusal)
        || HasLegacy(photos, "Intraoral", "lower");

    public static bool DeriveOpg(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Radiograph, SubtypeOpg)
        || HasLegacy(photos, "Radiograph", "OPG");

    public static bool DeriveLateralCeph(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Radiograph, SubtypeLateralCeph);

    public static bool DeriveCbct(IReadOnlyCollection<OrthoClinicalPhoto> photos) =>
        HasTag(photos, OrthoPhotoCategory.Radiograph, SubtypeCbct)
        || HasLegacy(photos, "Radiograph", "CBCT");
}
