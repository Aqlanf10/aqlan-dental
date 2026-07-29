using AqlanDentalPro.Application.DTOs.Ceph;

namespace AqlanDentalPro.Application.Services;

/// <summary>
/// Allowlisted labels for doctor-approved structured cephalometric diagnoses.
/// Unknown/free-text diagnosis values are intentionally not converted to tags.
/// </summary>
public static class CephClinicalTagCatalog
{
    private static readonly IReadOnlyDictionary<string, CephClinicalTagDto> Skeletal =
        new Dictionary<string, CephClinicalTagDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["Class I"] = Tag("skeletal:class-i", "هيكلي Class I", "skeletal"),
            ["Class II"] = Tag("skeletal:class-ii", "هيكلي Class II", "skeletal"),
            ["Class III"] = Tag("skeletal:class-iii", "هيكلي Class III", "skeletal"),
        };

    private static readonly IReadOnlyDictionary<string, CephClinicalTagDto> Vertical =
        new Dictionary<string, CephClinicalTagDto>(StringComparer.OrdinalIgnoreCase)
        {
            ["Normodivergent"] = Tag("vertical:normodivergent", "نمط رأسي طبيعي", "vertical"),
            ["Hyperdivergent"] = Tag("vertical:hyperdivergent", "نمط رأسي مرتفع", "vertical"),
            ["Hypodivergent"] = Tag("vertical:hypodivergent", "نمط رأسي منخفض", "vertical"),
        };

    private static readonly IReadOnlyDictionary<string, CephClinicalTagDto> Incisor =
        new Dictionary<string, CephClinicalTagDto>(StringComparer.Ordinal)
        {
            ["بروز للقواطع العلوية والسفلية"] = Tag("incisor:bimaxillary-protrusion", "بروز القواطع العلوية والسفلية", "incisor"),
            ["بروز في القاطعة العلوية"] = Tag("incisor:upper-protrusion", "بروز القاطعة العلوية", "incisor"),
            ["بروز في القاطعة السفلية"] = Tag("incisor:lower-protrusion", "بروز القاطعة السفلية", "incisor"),
            ["ارتداد للقواطع العلوية والسفلية"] = Tag("incisor:bimaxillary-retrusion", "ارتداد القواطع العلوية والسفلية", "incisor"),
            ["ارتداد في القاطعة العلوية"] = Tag("incisor:upper-retrusion", "ارتداد القاطعة العلوية", "incisor"),
            ["ارتداد في القاطعة السفلية"] = Tag("incisor:lower-retrusion", "ارتداد القاطعة السفلية", "incisor"),
            ["ميلان القواطع ضمن الحدود الطبيعية"] = Tag("incisor:normal", "ميلان قواطع طبيعي", "incisor"),
        };

    public static List<CephClinicalTagDto> Build(
        bool analysisApproved,
        bool diagnosisApproved,
        string? skeletalClass,
        string? verticalPattern,
        string? incisorInclination)
    {
        if (!analysisApproved || !diagnosisApproved) return [];

        var result = new List<CephClinicalTagDto>(3);
        AddKnown(result, Skeletal, skeletalClass);
        AddKnown(result, Vertical, verticalPattern);
        AddKnown(result, Incisor, incisorInclination);
        return result;
    }

    public static List<CephClinicalTagDto> All() =>
        Skeletal.Values.Concat(Vertical.Values).Concat(Incisor.Values)
            .Select(Clone)
            .ToList();

    public static bool IsKnownKey(string key) =>
        Skeletal.Values.Concat(Vertical.Values).Concat(Incisor.Values)
            .Any(tag => string.Equals(tag.Key, key, StringComparison.Ordinal));

    private static void AddKnown(
        ICollection<CephClinicalTagDto> target,
        IReadOnlyDictionary<string, CephClinicalTagDto> catalog,
        string? value)
    {
        if (value is not null && catalog.TryGetValue(value.Trim(), out var tag))
            target.Add(Clone(tag));
    }

    private static CephClinicalTagDto Tag(string key, string label, string group) =>
        new() { Key = key, Label = label, Group = group };

    private static CephClinicalTagDto Clone(CephClinicalTagDto tag) =>
        new() { Key = tag.Key, Label = tag.Label, Group = tag.Group };
}
