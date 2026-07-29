using AqlanDentalPro.Application.Interfaces.Services;
using System.Text.Json;

namespace AqlanDentalPro.Infrastructure.Services.Ai;

/// <summary>
/// Shared cephalometric-landmark prompt + response parsing used by every
/// <see cref="ICephLandmarkDraftProvider"/> implementation (Gemini, Anthropic, …).
///
/// The anatomical prompt IS the model's clinical intelligence, so keeping a single
/// copy means every provider draws the landmarks to the same definitions and the
/// same conservative confidence/omission rules — the orthodontist gets identical
/// behaviour no matter which AI provider the clinic has configured. All output is
/// still an UNSAVED draft that a human must review; nothing here saves or approves.
/// </summary>
internal static class CephLandmarkDraftShared
{
    /// <summary>The 27 landmarks the model is allowed to return. Anything else is dropped.</summary>
    public static readonly HashSet<string> AllowedKeys =
    [
        "S", "N", "Or", "Po", "ANS", "PNS", "A", "B", "Pog", "Gn", "Me", "Go",
        "Co", "Ar", "D", "Pm", "U1T", "U1A", "L1T", "L1A", "U6", "L6",
        "LS", "LI", "Pn", "Cm", "SPog",
    ];

    /// <summary>
    /// Parse a provider's JSON text into validated landmark points. Tolerates a
    /// ```json code fence, accepts either a bare array or {"landmarks":[…]}, drops
    /// unknown/duplicate keys and out-of-range coordinates, clamps confidence to
    /// 0..1, and trims reasoning to 200 chars. Identical rules for every provider.
    /// </summary>
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

            string? reasoning = null;
            if (item.TryGetProperty("reasoning", out var reasoningElement)
                && reasoningElement.ValueKind == JsonValueKind.String)
            {
                var r = reasoningElement.GetString();
                if (!string.IsNullOrWhiteSpace(r))
                    reasoning = r.Trim();
                if (reasoning is { Length: > 200 })
                    reasoning = reasoning[..200];
            }

            result.Add(new CephAiLandmarkPoint(key, x, y, confidence, reasoning));
        }

        return result;
    }

    public static string BuildPrompt(CephAiPrecision precision) =>
        $$"""
        You are creating an UNSAVED first-pass landmark draft for a lateral cephalometric radiograph.
        Return JSON only. Do not include diagnosis, prose, markdown, patient identity, or treatment advice.
        Human orthodontist review and manual correction are mandatory.

        IMAGE ORIENTATION:
        - This is a standard lateral cephalogram. The patient faces RIGHT (anterior is on the right side of the image, posterior is on the left).
        - x=0 is the LEFT (posterior) edge of the image, x=1000 is the RIGHT (anterior) edge.
        - y=0 is the TOP of the image (superior), y=1000 is the BOTTOM (inferior).

        ANATOMICAL LANDMARKS — locate each at the named anatomical definition:
        - S  (Sella): geometric center of sella turcica (the pituitary fossa), midpoint of the sella outline.
        - N  (Nasion): most anterior point of the frontonasal suture on the midsagittal plane.
        - Or (Orbitale): lowest point on the infraorbital margin (anatomical left side, the side closer to the film).
        - Po (Porion): most superior point of the external auditory meatus (anatomical left side).
        - ANS (Anterior Nasal Spine): tip of the bony anterior nasal spine of the maxilla.
        - PNS (Posterior Nasal Spine): tip of the posterior nasal spine, the most posterior point of the hard palate.
        - A  (Point A / A-Point): most anterior point on the maxillary alveolar process, between ANS and the crest of the maxillary central incisor alveolus, on the curvature.
        - B  (Point B / B-Point): most anterior point on the mandibular alveolar process, between Pogonion and the crest of the mandibular central incisor alveolus, on the curvature.
        - Pog (Pogonion): most anterior point of the bony chin (mandibular symphysis), on the midsagittal plane.
        - Gn (Gnathion): midpoint between Pogonion and Menton, on the anterior border of the mandibular symphysis (often at the bisector of the facial plane and mandibular plane).
        - Me (Menton): most inferior point of the mandibular symphysis, on the midsagittal plane.
        - Go (Gonion): the most posterior-inferior point at the angle of the mandible, at the curvature where the body meets the ramus (constructed as the bisector of the posterior and inferior mandibular borders).
        - Co (Condylion): most superior-posterior point on the head of the mandibular condyle (use the condyle farther from the film — the right side — when both are visible).
        - Ar (Articulare): intersection of the inferior cranial base surface and the posterior border of the mandibular condyle/ramus.
        - D  (Point D): midpoint of the mandibular symphysis on the labial surface, halfway between B and the chin bone (between B and Pogonion, on the anterior surface of the symphysis).
        - Pm (Protuberans Menti): anterior limit of the chin, where the curvature of the chin changes from concave to convex (above Pogonion on the anterior mandibular surface).
        - U1T (Upper Incisor Tip): incisal edge tip of the most anterior maxillary central incisor.
        - U1A (Upper Incisor Apex): root apex of the same maxillary central incisor as U1T.
        - L1T (Lower Incisor Tip): incisal edge tip of the most anterior mandibular central incisor.
        - L1A (Lower Incisor Apex): root apex of the same mandibular central incisor as L1T.
        - U6 (Upper First Molar): mesial cusp tip of the maxillary first molar occlusal surface (the most mesial occlusal cusp of the upper first molar).
        - L6 (Lower First Molar): mesial cusp tip of the mandibular first molar occlusal surface (occluding with U6; place at the mesiobuccal cusp tip).
        - LS (Labiale Superioris): most anterior point on the margin of the upper lip (soft tissue).
        - LI (Labiale Inferioris): most anterior point on the margin of the lower lip (soft tissue).
        - Pn (Pronasale): most anterior point on the tip of the nose (soft tissue).
        - Cm (Columella): most anterior point on the columella of the nose (where the nose meets the upper lip, soft tissue).
        - SPog (Soft-Tissue Pogonion): most anterior point of the soft-tissue chin contour (on the skin profile, anterior to the bony Pogonion).

        COORDINATE RULES:
        - x and y are normalized values from 0 to 1000 (integers or floats).
        - Use the orientation rules above: x=0 posterior/left, x=1000 anterior/right, y=0 top/superior, y=1000 bottom/inferior.

        CONFIDENCE CALIBRATION (0..1, conservative):
        - < 0.3  : anatomy is obscured by overlap, motion blur, or poor contrast — prefer to OMIT the landmark.
        - 0.3-0.7: anatomy is partially visible but the exact point is uncertain.
        - > 0.7  : anatomy is clearly visible and the point is anatomically unambiguous.

        REASONING (optional, 1 short sentence per landmark): briefly state the anatomical landmark you matched
        (e.g. "center of sella outline", "lowest point of infraorbital margin"). Keep under 200 characters.
        This helps the orthodontist understand WHY the model placed the point there.

        OUTPUT FORMAT:
        {"landmarks":[{"key":"S","x":500,"y":300,"confidence":0.85,"reasoning":"center of sella turcica outline"}]}
        Omit a landmark rather than guessing when the anatomy is not visible or confidence would be < 0.3.

        {{(precision == CephAiPrecision.High ?
        "PRECISION MODE: HIGH. Take extra time. For EACH landmark, cross-check it against its anatomical reference points (e.g. for Go, verify the posterior and inferior mandibular borders actually meet at the placed point; for A, verify ANS and the maxillary incisor alveolar crest are on either side). Only return a landmark if you can place it with confidence > 0.5. If a landmark is uncertain after cross-checking, OMIT it — do not include low-confidence guesses." :
        "PRECISION MODE: DRAFT. Fast first pass. Return every landmark you can place with confidence >= 0.3. The orthodontist will refine manually.")}}
        """;

    public static string BuildRefinePrompt(CephLandmarkRefineTarget target) =>
        $$"""
        You are REFINING the position of a single cephalometric landmark on a lateral cephalometric radiograph.
        The image is a standard lateral ceph: patient faces RIGHT (anterior = right side of image, posterior = left side).
        x=0 is the LEFT (posterior) edge, x=1000 is the RIGHT (anterior) edge. y=0 is TOP (superior), y=1000 is BOTTOM (inferior).

        The orthodontist believes the current position of landmark "{{target.Key}}" is approximately
        (x={{target.XNormalized:F1}}, y={{target.YNormalized:F1}}) on the 0..1000 grid, but wants the AI to
        re-evaluate it. Look at the area around that position AND at the surrounding anatomical references,
        then return the most anatomically accurate refined position for "{{target.Key}}".

        Refer to the standard anatomical definition of "{{target.Key}}" when refining. Cross-check the proposed
        position against adjacent landmarks (e.g. for Go, confirm it lies at the curvature where the posterior
        and inferior mandibular borders meet; for S, confirm it is the geometric center of the sella outline).

        Return JSON ONLY, exactly one landmark object, with this shape:
        {"landmarks":[{"key":"{{target.Key}}","x":0,"y":0,"confidence":0.0,"reasoning":"short Arabic or English note"}]}

        Confidence calibration:
        - < 0.3  : cannot locate the landmark in the current image — return confidence < 0.3 and the unchanged position.
        - 0.3-0.7: partially confident.
        - > 0.7  : clearly visible and anatomically unambiguous.

        The "reasoning" field (1 short sentence, < 200 chars) should state which anatomical feature you matched.
        Do NOT include any other landmark. Do NOT include prose outside the JSON.
        """;
}
