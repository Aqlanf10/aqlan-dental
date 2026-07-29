// ---------------------------------------------------------------------------
// Frontal facial photo analysis — proportion & symmetry measurements.
// Honest and scale-independent: every value is a unitless RATIO computed from
// points placed on a frontal photo, so no calibration and no AI are involved.
// Norms follow the neoclassical facial canons (fifths / thirds / symmetry).
// ---------------------------------------------------------------------------

import { dist } from "@/lib/cephMath";

type Pt = { x: number; y: number };

export interface FrontalLandmarkDef {
  key: string;
  nameAr: string;
}

/** Frontal soft-tissue landmarks (R/L are the patient's right/left). */
export const FRONTAL_LANDMARKS: FrontalLandmarkDef[] = [
  { key: "Tr",   nameAr: "منبت الشعر (Trichion)" },
  { key: "G",    nameAr: "الجبهة بين الحاجبين (Glabella)" },
  { key: "ExR",  nameAr: "الزاوية الخارجية للعين اليمنى" },
  { key: "EnR",  nameAr: "الزاوية الداخلية للعين اليمنى" },
  { key: "EnL",  nameAr: "الزاوية الداخلية للعين اليسرى" },
  { key: "ExL",  nameAr: "الزاوية الخارجية للعين اليسرى" },
  { key: "PR",   nameAr: "حدقة العين اليمنى" },
  { key: "PL",   nameAr: "حدقة العين اليسرى" },
  { key: "AlR",  nameAr: "جناح الأنف الأيمن (Alare)" },
  { key: "AlL",  nameAr: "جناح الأنف الأيسر (Alare)" },
  { key: "ChR",  nameAr: "زاوية الفم اليمنى (Cheilion)" },
  { key: "ChL",  nameAr: "زاوية الفم اليسرى (Cheilion)" },
  { key: "Sn",   nameAr: "تحت الأنف (Subnasale)" },
  { key: "Me",   nameAr: "أسفل الذقن (Menton)" },
];

export interface FrontalNorm {
  normal: number;
  sd: number;
  nameAr: string;
  /** Interpretation when the ratio is meaningfully ABOVE / BELOW the norm. */
  interpHi: string;
  interpLo: string;
}

export const FRONTAL_NORMS: Record<string, FrontalNorm> = {
  EyeSymmetry: {
    normal: 1, sd: 0.05, nameAr: "تناظر عرض العينين (يمين÷يسار)",
    interpHi: "العين اليمنى أعرض — لا تناظر",
    interpLo: "العين اليسرى أعرض — لا تناظر",
  },
  IntercanthalEye: {
    normal: 1, sd: 0.1, nameAr: "المسافة بين العينين ÷ عرض العين",
    interpHi: "تباعد بين العينين (telecanthus)",
    interpLo: "تقارب بين العينين",
  },
  NasalWidth: {
    normal: 1, sd: 0.1, nameAr: "عرض الأنف ÷ المسافة بين العينين",
    interpHi: "قاعدة أنف عريضة",
    interpLo: "قاعدة أنف ضيقة",
  },
  MouthNose: {
    normal: 1.5, sd: 0.2, nameAr: "عرض الفم ÷ عرض الأنف",
    interpHi: "فم عريض نسبةً للأنف",
    interpLo: "فم ضيّق نسبةً للأنف",
  },
  MouthInterpupillary: {
    normal: 1, sd: 0.1, nameAr: "عرض الفم ÷ المسافة بين الحدقتين",
    interpHi: "فم عريض نسبةً للحدقتين",
    interpLo: "فم ضيّق نسبةً للحدقتين",
  },
  UpperMiddleThird: {
    normal: 1, sd: 0.1, nameAr: "الثلث العلوي ÷ الأوسط (Tr–G ÷ G–Sn)",
    interpHi: "الثلث العلوي أطول",
    interpLo: "الثلث العلوي أقصر",
  },
  LowerMiddleThird: {
    normal: 1, sd: 0.1, nameAr: "الثلث السفلي ÷ الأوسط (Sn–Me ÷ G–Sn)",
    interpHi: "الثلث السفلي أطول (وجه طويل)",
    interpLo: "الثلث السفلي أقصر (وجه قصير)",
  },
  MouthCant: {
    normal: 0, sd: 0.03, nameAr: "ميل خط الفم (انحراف زوايا الفم)",
    interpHi: "زاوية الفم اليمنى أخفض — ميل",
    interpLo: "زاوية الفم اليسرى أخفض — ميل",
  },
};

export type FrontalSeverity = "normal" | "mild" | "severe";

export interface FrontalMeasurement {
  key: string;
  nameAr: string;
  value: number | null;
  normal: number;
  sd: number;
  deviation: number | null;
  severity: FrontalSeverity;
  interpretationAr?: string;
}

function r2(v: number): number { return +v.toFixed(2); }

/** Computes the frontal proportion/symmetry measurements from placed landmarks. */
export function computeFrontalMeasurements(pts: Record<string, Pt>): FrontalMeasurement[] {
  const has = (...keys: string[]) => keys.every((k) => pts[k]);
  const out: FrontalMeasurement[] = [];

  const add = (key: string, value: number | null) => {
    const n = FRONTAL_NORMS[key];
    const deviation = value !== null ? r2(value - n.normal) : null;
    const ratio = deviation !== null && n.sd > 0 ? Math.abs(deviation / n.sd) : 0;
    const severity: FrontalSeverity =
      deviation === null ? "normal" : ratio <= 1 ? "normal" : ratio <= 2 ? "mild" : "severe";
    let interpretationAr: string | undefined;
    if (deviation !== null) {
      interpretationAr = severity === "normal"
        ? "ضمن الحدود الطبيعية"
        : deviation > 0 ? n.interpHi : n.interpLo;
    }
    out.push({ key, nameAr: n.nameAr, value, normal: n.normal, sd: n.sd, deviation, severity, interpretationAr });
  };

  const eyeR = has("ExR", "EnR") ? dist(pts.ExR, pts.EnR) : null;
  const eyeL = has("ExL", "EnL") ? dist(pts.ExL, pts.EnL) : null;
  const intercanthal = has("EnR", "EnL") ? dist(pts.EnR, pts.EnL) : null;
  const nasal = has("AlR", "AlL") ? dist(pts.AlR, pts.AlL) : null;
  const mouth = has("ChR", "ChL") ? dist(pts.ChR, pts.ChL) : null;
  const interpupillary = has("PR", "PL") ? dist(pts.PR, pts.PL) : null;
  const upper = has("Tr", "G") ? dist(pts.Tr, pts.G) : null;
  const middle = has("G", "Sn") ? dist(pts.G, pts.Sn) : null;
  const lower = has("Sn", "Me") ? dist(pts.Sn, pts.Me) : null;

  add("EyeSymmetry", eyeR !== null && eyeL !== null && eyeL > 0 ? r2(eyeR / eyeL) : null);
  const meanEye = eyeR !== null && eyeL !== null ? (eyeR + eyeL) / 2 : null;
  add("IntercanthalEye", intercanthal !== null && meanEye !== null && meanEye > 0 ? r2(intercanthal / meanEye) : null);
  add("NasalWidth", nasal !== null && intercanthal !== null && intercanthal > 0 ? r2(nasal / intercanthal) : null);
  add("MouthNose", mouth !== null && nasal !== null && nasal > 0 ? r2(mouth / nasal) : null);
  add("MouthInterpupillary", mouth !== null && interpupillary !== null && interpupillary > 0 ? r2(mouth / interpupillary) : null);
  add("UpperMiddleThird", upper !== null && middle !== null && middle > 0 ? r2(upper / middle) : null);
  add("LowerMiddleThird", lower !== null && middle !== null && middle > 0 ? r2(lower / middle) : null);
  // Mouth cant: signed vertical offset of the commissures, normalised by mouth width.
  add("MouthCant", has("ChR", "ChL") && mouth !== null && mouth > 0 ? r2((pts.ChR.y - pts.ChL.y) / mouth) : null);

  return out;
}

/** Overall symmetry/proportion verdict from the computed measurements. */
export function frontalSummary(measurements: FrontalMeasurement[]): string | null {
  const scored = measurements.filter((m) => m.value !== null);
  if (scored.length === 0) return null;
  const abnormal = scored.filter((m) => m.severity !== "normal").length;
  if (abnormal === 0) return "النسب والتناظر ضمن الحدود الطبيعية";
  return `${abnormal} قياس خارج النطاق الطبيعي — يتطلب تقييم الأخصائي`;
}
