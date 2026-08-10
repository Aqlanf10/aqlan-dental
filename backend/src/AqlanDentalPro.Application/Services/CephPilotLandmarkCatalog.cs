namespace AqlanDentalPro.Application.Services;

public static class CephPilotLandmarkCatalog
{
    public static readonly IReadOnlyList<string> CoreKeys =
    [
        "S", "N", "Or", "Po", "ANS", "PNS", "A", "B", "Pog", "Gn", "Me", "Go",
        "Co", "Ar", "D", "Pm", "U1T", "U1A", "L1T", "L1A", "LS", "LI", "Pn", "Cm",
    ];

    public static readonly IReadOnlyList<string> OptionalKeys = ["SPog", "U6", "L6"];
    public static readonly string[] AllOrderedKeys = CoreKeys.Concat(OptionalKeys).ToArray();
    public static readonly IReadOnlySet<string> AllKeys = AllOrderedKeys.ToHashSet(StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> CoreKeySet = CoreKeys.ToHashSet(StringComparer.Ordinal);
}
