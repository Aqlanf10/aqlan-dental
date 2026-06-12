namespace AqlanDentalPro.API.Controllers;

/// <summary>
/// Phase 3 — structured clinical examination helpers:
/// case-insensitive validation/normalization of enum-like string fields against
/// their allowed sets, plus numeric range sanity checks (mm / percent).
/// Same pattern as <see cref="OrthoPhotoRecords"/>.
/// </summary>
public static class OrthoClinicalExamFields
{
    public static readonly IReadOnlyList<string> AngleClasses =
        new[] { "ClassI", "ClassII", "ClassIII" };

    public static readonly IReadOnlyList<string> IncisorRelations =
        new[] { "ClassI", "ClassIIDiv1", "ClassIIDiv2", "ClassIII" };

    public static readonly IReadOnlyList<string> CrossbiteTypes =
        new[] { "Anterior", "PosteriorUnilateralRight", "PosteriorUnilateralLeft", "PosteriorBilateral" };

    public static readonly IReadOnlyList<string> CurveOfSpeeValues =
        new[] { "Normal", "Deep", "Reverse" };

    public static readonly IReadOnlyList<string> ArchForms =
        new[] { "Ovoid", "Tapered", "Square" };

    public static readonly IReadOnlyList<string> LipCompetenceGrades =
        new[] { "Competent", "PotentiallyCompetent", "Incompetent" };

    public static readonly IReadOnlyList<string> NasolabialAngles =
        new[] { "Normal", "Acute", "Obtuse" };

    public static readonly IReadOnlyList<string> ChinPositions =
        new[] { "Normal", "Prominent", "Retruded" };

    public static readonly IReadOnlyList<string> OralHygieneValues =
        new[] { "Good", "Fair", "Poor" };

    /// <summary>
    /// Validates a value against an allowed set (case-insensitive, trimmed).
    /// Null/blank is always valid and normalizes to null.
    /// Returns true with the canonical value on success.
    /// </summary>
    public static bool TryNormalize(string? value, IReadOnlyList<string> allowed, out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        var match = allowed.FirstOrDefault(a =>
            string.Equals(a, value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null) return false;
        normalized = match;
        return true;
    }

    /// <summary>Null is valid; otherwise the value must lie within [min, max].</summary>
    public static bool IsInRange(decimal? value, decimal min, decimal max) =>
        value is null || (value >= min && value <= max);
}
