import type { CephLandmark, CephMeasurement, LandmarkGroup } from "@/types/ceph";

export const PA_LANDMARK_DEFS: Record<string, { nameAr: string; group: LandmarkGroup; color: string }> = {
  Cg:  { nameAr: "كريستا غالي (Cg)", group: "cranial", color: "#60A5FA" },
  ZR:  { nameAr: "جبهي وجني أيمن (ZR)", group: "cranial", color: "#A78BFA" },
  ZL:  { nameAr: "جبهي وجني أيسر (ZL)", group: "cranial", color: "#A78BFA" },
  ANS: { nameAr: "الشوكة الأنفية الأمامية", group: "maxilla", color: "#FB923C" },
  JR:  { nameAr: "جوغال أيمن (JR)", group: "maxilla", color: "#FB923C" },
  JL:  { nameAr: "جوغال أيسر (JL)", group: "maxilla", color: "#FB923C" },
  AgR: { nameAr: "أنتيغونيون أيمن (AgR)", group: "mandible", color: "#F87171" },
  AgL: { nameAr: "أنتيغونيون أيسر (AgL)", group: "mandible", color: "#F87171" },
  Me:  { nameAr: "منتون (Me)", group: "mandible", color: "#F87171" },
  UDM: { nameAr: "منتصف الأسنان العلوية", group: "dental", color: "#34D399" },
  LDM: { nameAr: "منتصف الأسنان السفلية", group: "dental", color: "#10B981" },
  U6R: { nameAr: "الرحى العلوية اليمنى", group: "dental", color: "#2DD4BF" },
  U6L: { nameAr: "الرحى العلوية اليسرى", group: "dental", color: "#2DD4BF" },
  L6R: { nameAr: "الرحى السفلية اليمنى", group: "dental", color: "#14B8A6" },
  L6L: { nameAr: "الرحى السفلية اليسرى", group: "dental", color: "#14B8A6" },
};

export const PA_LANDMARK_ORDER = [
  "Cg", "ZR", "ZL", "ANS", "JR", "JL", "AgR", "AgL", "Me", "UDM", "LDM", "U6R", "U6L", "L6R", "L6L",
] as const;

export const PA_LANDMARK_GROUPS = [
  { key: "reference", label: "المراجع القحفية", keys: ["Cg", "ZR", "ZL"] },
  { key: "maxilla", label: "الفك العلوي", keys: ["ANS", "JR", "JL"] },
  { key: "mandible", label: "الفك السفلي", keys: ["AgR", "AgL", "Me"] },
  { key: "dental", label: "الأسنان والمستويات الإطباقية", keys: ["UDM", "LDM", "U6R", "U6L", "L6R", "L6L"] },
] as const;

type Point = { x: number; y: number };

const NAMES: Record<string, string> = {
  "PA-JJ-Width": "عرض الفك العلوي (J–J)",
  "PA-AgAg-Width": "عرض الفك السفلي (Ag–Ag)",
  "PA-Width-Differential": "فرق العرض السفلي − العلوي",
  "PA-ANS-MSR": "انحراف ANS عن الخط الناصف",
  "PA-UDM-MSR": "انحراف منتصف الأسنان العلوية",
  "PA-LDM-MSR": "انحراف منتصف الأسنان السفلية",
  "PA-Me-MSR": "انحراف الذقن (Me)",
  "PA-Maxillary-Cant": "ميل المستوى الإطباقي العلوي",
  "PA-Mandibular-Cant": "ميل المستوى الإطباقي السفلي",
  "PA-J-Vertical-Asymmetry": "عدم التناظر الرأسي عند J",
  "PA-Ag-Vertical-Asymmetry": "عدم التناظر الرأسي عند Ag",
};

function observed(name: string, value: number, unit: "°" | "mm"): CephMeasurement {
  const interpretation = name === "PA-JJ-Width" || name === "PA-AgAg-Width"
    ? "قياس عرض وصفي — قارنه بمرجع مناسب لعمر المريض وجنسه وطريقة التصوير."
    : name === "PA-Width-Differential"
      ? "القيمة الموجبة تعني أن عرض الفك السفلي أكبر من عرض الفك العلوي."
      : name.includes("Cant") || name.includes("Vertical-Asymmetry")
        ? "القيمة الموجبة تعني أن الجانب الأيسر للمريض أخفض من الأيمن."
        : "القيمة الموجبة تعني انحرافًا نحو يسار المريض، والسالبة نحو يمينه.";
  return {
    name,
    nameAr: NAMES[name] ?? name,
    value: Math.round(value * 10) / 10,
    normal: null,
    stdDev: null,
    unit,
    deviation: null,
    severity: "unclassified",
    direction: "within",
    analysisGroup: "pa",
    interpretationAr: interpretation,
  };
}

export function buildPaMeasurements(
  landmarks: readonly CephLandmark[],
  pixelsPerMm: number | null,
): CephMeasurement[] {
  const points = Object.fromEntries(landmarks.map((p) => [p.key, { x: p.x, y: p.y }])) as Record<string, Point>;
  const zr = points.ZR;
  const zl = points.ZL;
  if (!zr || !zl) return [];
  const dx = zl.x - zr.x;
  const dy = zl.y - zr.y;
  const length = Math.hypot(dx, dy);
  if (length < 1e-9) return [];
  const hx = dx / length;
  const hy = dy / length;
  const vx = -hy;
  const vy = hx;
  const h = (p: Point, origin: Point) => (p.x - origin.x) * hx + (p.y - origin.y) * hy;
  const v = (p: Point, origin: Point) => (p.x - origin.x) * vx + (p.y - origin.y) * vy;
  const dist = (a: Point, b: Point) => Math.hypot(a.x - b.x, a.y - b.y);
  const cant = (right: Point, left: Point) => Math.atan2(v(left, right), h(left, right)) * 180 / Math.PI;
  const calibrated = pixelsPerMm !== null && pixelsPerMm > 0;
  const out: CephMeasurement[] = [];

  if (calibrated && points.JR && points.JL) out.push(observed("PA-JJ-Width", dist(points.JR, points.JL) / pixelsPerMm, "mm"));
  if (calibrated && points.AgR && points.AgL) out.push(observed("PA-AgAg-Width", dist(points.AgR, points.AgL) / pixelsPerMm, "mm"));
  if (calibrated && points.JR && points.JL && points.AgR && points.AgL) {
    out.push(observed("PA-Width-Differential", (dist(points.AgR, points.AgL) - dist(points.JR, points.JL)) / pixelsPerMm, "mm"));
  }
  if (calibrated && points.Cg) {
    for (const key of ["ANS", "UDM", "LDM", "Me"] as const) {
      if (points[key]) out.push(observed(`PA-${key}-MSR`, h(points[key], points.Cg) / pixelsPerMm, "mm"));
    }
  }
  if (points.U6R && points.U6L) out.push(observed("PA-Maxillary-Cant", cant(points.U6R, points.U6L), "°"));
  if (points.L6R && points.L6L) out.push(observed("PA-Mandibular-Cant", cant(points.L6R, points.L6L), "°"));
  if (calibrated && points.JR && points.JL) out.push(observed("PA-J-Vertical-Asymmetry", v(points.JL, points.JR) / pixelsPerMm, "mm"));
  if (calibrated && points.AgR && points.AgL) out.push(observed("PA-Ag-Vertical-Asymmetry", v(points.AgL, points.AgR) / pixelsPerMm, "mm"));
  return out;
}
