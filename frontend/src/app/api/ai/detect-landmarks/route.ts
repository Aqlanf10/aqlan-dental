import { NextRequest, NextResponse } from "next/server";
import ZAI from "z-ai-web-dev-sdk";

// ──────────────────────────────────────────────────────────────────────────────
// VLM-based Cephalometric Landmark Detection Fallback
// ──────────────────────────────────────────────────────────────────────────────
// When no ONNX model is available in the .NET backend, this API route uses
// the z-ai-web-dev-sdk VLM (Vision Language Model) to analyze cephalometric
// X-ray images and identify the 24 landmark coordinates.
//
// POST /api/ai/detect-landmarks
// Body: { imageBase64: string, imageWidth: number, imageHeight: number }
// ──────────────────────────────────────────────────────────────────────────────

const LANDMARK_KEYS = [
  "S","N","Or","Po","ANS","PNS","A","B","Pog","Gn",
  "Me","Go","Co","Ar","D","Pm","U1T","U1A","L1T","L1A",
  "LS","LI","Pn","Cm"
] as const;

const LANDMARK_DESCRIPTIONS: Record<string, string> = {
  S:   "Sella (center of pituitary fossa)",
  N:   "Nasion (frontonasal suture)",
  Or:  "Orbitale (lowest point on infraorbital margin)",
  Po:  "Porion (uppermost point on bony external auditory meatus)",
  ANS: "Anterior Nasal Spine",
  PNS: "Posterior Nasal Spine",
  A:   "Point A (subspinale — deepest concavity of maxilla between ANS and alveolar crest)",
  B:   "Point B (supramentale — deepest concavity of mandible between alveolar crest and pogonion)",
  Pog: "Pogonion (most anterior point on chin)",
  Gn:  "Gnathion (most anteroinferior point on chin, between Pog and Me)",
  Me:  "Menton (lowest point on mandibular symphysis)",
  Go:  "Gonion (most posterior inferior point on angle of mandible)",
  Co:  "Condylion (most superior posterior point on mandibular condyle)",
  Ar:  "Articulare (junction of posterior cranial base and condylar neck)",
  D:   "Point D (geometric center of mandibular body between B and Pog)",
  Pm:  "Protuberance menti (transition between lower lip and chin)",
  U1T: "Upper incisor tip (incisal edge of most anterior upper central incisor)",
  U1A: "Upper incisor apex (root apex of most anterior upper central incisor)",
  L1T: "Lower incisor tip (incisal edge of most anterior lower central incisor)",
  L1A: "Lower incisor apex (root apex of most anterior lower central incisor)",
  LS:  "Upper lip (Labrale superior — most anterior point on upper lip)",
  LI:  "Lower lip (Labrale inferior — most anterior point on lower lip)",
  Pn:  "Pronasale (most anterior point on nose tip)",
  Cm:  "Columella (most inferior anterior point on columella of nose)",
};

const SYSTEM_PROMPT = `You are a specialized AI assistant for orthodontic cephalometric analysis. You will be given a lateral cephalometric X-ray image. Your task is to identify the exact pixel coordinates of 24 anatomical landmarks on this image.

IMPORTANT RULES:
1. Return ONLY a valid JSON object — no markdown, no code fences, no explanation.
2. The JSON must have this exact structure:
{
  "landmarks": {
    "S": { "x": <number>, "y": <number>, "confidence": <0-1> },
    "N": { "x": <number>, "y": <number>, "confidence": <0-1> },
    ... (all 24 keys)
  }
}
3. Coordinates must be in the original image coordinate system (0 to imageWidth for x, 0 to imageHeight for y).
4. The origin (0,0) is the TOP-LEFT corner. Y increases downward.
5. Confidence should be between 0 and 1, reflecting how certain you are about each landmark position.
6. You MUST identify ALL 24 landmarks. If uncertain, provide your best estimate with lower confidence.

Landmark definitions:
${Object.entries(LANDMARK_DESCRIPTIONS)
  .map(([k, v]) => `- ${k}: ${v}`)
  .join("\n")}

Image dimensions will be provided with each request. Map your coordinates accordingly.`;

export async function POST(request: NextRequest) {
  try {
    const body = await request.json();
    const { imageBase64, imageWidth, imageHeight } = body;

    if (!imageBase64 || !imageWidth || !imageHeight) {
      return NextResponse.json(
        { error: "Missing required fields: imageBase64, imageWidth, imageHeight" },
        { status: 400 }
      );
    }

    // Create ZAI instance
    const zai = await ZAI.create();

    // Prepare the image URL (base64 data URL)
    const imageUrl = imageBase64.startsWith("data:")
      ? imageBase64
      : `data:image/jpeg;base64,${imageBase64}`;

    // Call VLM with the cephalometric image
    const response = await zai.chat.completions.createVision({
      model: "gpt-4o",
      messages: [
        {
          role: "system",
          content: SYSTEM_PROMPT,
        },
        {
          role: "user",
          content: [
            {
              type: "text",
              text: `Identify all 24 cephalometric landmarks on this lateral cephalometric X-ray. The image dimensions are ${imageWidth}x${imageHeight} pixels. Return the JSON with coordinates in this image space.`,
            },
            {
              type: "image_url",
              image_url: { url: imageUrl },
            },
          ],
        },
      ],
      stream: false,
    });

    // Parse the VLM response
    const content =
      response?.choices?.[0]?.message?.content ??
      response?.content ??
      "";

    // Extract JSON from the response (handle markdown code fences)
    let jsonStr = content;
    const jsonMatch = content.match(/```(?:json)?\s*([\s\S]*?)```/);
    if (jsonMatch) {
      jsonStr = jsonMatch[1].trim();
    }

    // Try to find a JSON object in the response
    const braceMatch = jsonStr.match(/\{[\s\S]*\}/);
    if (!braceMatch) {
      return NextResponse.json(
        { error: "VLM did not return valid JSON", raw: content },
        { status: 422 }
      );
    }

    const parsed = JSON.parse(braceMatch[0]);
    const vlmLandmarks = parsed.landmarks ?? parsed;

    // Convert to our standard format
    const landmarks = LANDMARK_KEYS.map((key) => {
      const lm = vlmLandmarks[key];
      return {
        key,
        nameAr: getArabicName(key),
        x: typeof lm?.x === "number" ? Math.round(lm.x * 10) / 10 : 0,
        y: typeof lm?.y === "number" ? Math.round(lm.y * 10) / 10 : 0,
        confidence:
          typeof lm?.confidence === "number"
            ? Math.round(lm.confidence * 1000) / 1000
            : 0.5,
        isAiPlaced: true,
      };
    });

    // Filter out landmarks with zero coordinates (not detected)
    const validLandmarks = landmarks.filter((l) => l.x > 0 || l.y > 0);
    const avgConfidence =
      validLandmarks.length > 0
        ? validLandmarks.reduce((sum, l) => sum + l.confidence, 0) /
          validLandmarks.length
        : 0;

    return NextResponse.json({
      landmarks,
      modelType: "vlm",
      isModelLoaded: true,
      modelVersion: "gpt-4o-vlm",
      count: validLandmarks.length,
      averageConfidence: Math.round(avgConfidence * 1000) / 1000,
    });
  } catch (error: unknown) {
    console.error("VLM landmark detection error:", error);
    const message =
      error instanceof Error ? error.message : "VLM detection failed";
    return NextResponse.json(
      { error: "فشل الكشف عن النقاط باستخدام VLM", details: message },
      { status: 500 }
    );
  }
}

function getArabicName(key: string): string {
  const names: Record<string, string> = {
    S: "السرج",
    N: "الناسيون",
    Or: "قاع المدار",
    Po: "قمة المسمع",
    ANS: "الشوكة الأنفية الأمامية",
    PNS: "الشوكة الأنفية الخلفية",
    A: "النقطة A",
    B: "النقطة B",
    Pog: "الذقن البارز",
    Gn: "الذقن",
    Me: "الأسفل",
    Go: "زاوية الفك",
    Co: "رأس اللقمة",
    Ar: "المفصل",
    D: "النقطة D",
    Pm: "بروز الذقن",
    U1T: "طرف القاطع العلوي",
    U1A: "قمة القاطع العلوي",
    L1T: "طرف القاطع السفلي",
    L1A: "قمة القاطع السفلي",
    LS: "الشفة العلوية",
    LI: "الشفة السفلية",
    Pn: "طرف الأنف",
    Cm: "قاعدة الأنف",
  };
  return names[key] ?? key;
}
