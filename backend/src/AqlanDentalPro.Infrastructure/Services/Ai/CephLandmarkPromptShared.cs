using AqlanDentalPro.Application.Interfaces.Services;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services.Ai;

/// <summary>
/// Landmark key set, prompt text, and JSON-array parsing shared by every
/// ICephLandmarkDraftProvider implementation so the 24-key contract and
/// coordinate rules can't drift between vendors.
/// </summary>
internal static class CephLandmarkPromptShared
{
    public static readonly HashSet<string> AllowedKeys =
    [
        "S", "N", "Or", "Po", "ANS", "PNS", "A", "B", "Pog", "Gn", "Me", "Go",
        "Co", "Ar", "D", "Pm", "U1T", "U1A", "L1T", "L1A", "LS", "LI", "Pn", "Cm",
    ];

    public static List<CephAiLandmarkPoint> ParsePoints(string json)
    {
        var cleaned = json.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine >= 0 && lastFence > firstNewLine)
                cleaned = cleaned[(firstNewLine + 1)..lastFence].Trim();
        }

        using var document = JsonDocument.Parse(cleaned);
        var root = document.RootElement;
        var array = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("landmarks", out var landmarks)
                ? landmarks
                : throw new JsonException("landmarks array missing");

        var result = new List<CephAiLandmarkPoint>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            if (!item.TryGetProperty("key", out var keyElement))
                continue;
            var key = keyElement.GetString();
            if (key is null || !AllowedKeys.Contains(key) || !seen.Add(key))
                continue;
            if (!item.TryGetProperty("x", out var xElement)
                || !item.TryGetProperty("y", out var yElement)
                || !xElement.TryGetDouble(out var x)
                || !yElement.TryGetDouble(out var y))
                continue;
            if (x is < 0 or > 1000 || y is < 0 or > 1000)
                continue;

            double? confidence = null;
            if (item.TryGetProperty("confidence", out var confidenceElement)
                && confidenceElement.TryGetDouble(out var confidenceValue))
                confidence = Math.Clamp(confidenceValue, 0, 1);

            result.Add(new CephAiLandmarkPoint(key, x, y, confidence));
        }

        return result;
    }

    public static string BuildPrompt() =>
        """
        You are creating an UNSAVED first-pass landmark draft for a lateral cephalometric radiograph.
        Return JSON only. Do not include diagnosis, prose, markdown, patient identity, or treatment advice.
        Human orthodontist review and manual correction are mandatory.

        Locate as many of these exact cephalometric landmarks as are visually defensible:
        S, N, Or, Po, ANS, PNS, A, B, Pog, Gn, Me, Go, Co, Ar, D, Pm,
        U1T, U1A, L1T, L1A, LS, LI, Pn, Cm.

        Coordinate rules:
        - x and y are normalized integers/floats from 0 to 1000.
        - x=0 is the left image edge, x=1000 is the right edge.
        - y=0 is the top image edge, y=1000 is the bottom edge.
        - confidence is 0 to 1 and must be conservative.
        - omit a point rather than guessing when anatomy is not visible.

        Output:
        {"landmarks":[{"key":"S","x":0,"y":0,"confidence":0.0}]}
        """;
}
