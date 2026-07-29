// ---------------------------------------------------------------------------
// Profile (lateral) facial photo analysis — soft-tissue measurements.
// Honest and scale-independent: every value is an ANGLE computed from points the
// orthodontist places on the photo, so no calibration and no AI are involved.
// Norms follow Legan & Burstone soft-tissue cephalometrics.
// ---------------------------------------------------------------------------

import { angle3 } from "@/lib/cephMath";

type Pt = { x: number; y: number };

export interface ProfileLandmarkDef {
  key: string;
  nameAr: string;
}

/** Soft-tissue profile landmarks placed on a lateral facial photo. */
export const PROFILE_LANDMARKS: ProfileLandmarkDef[] = [
  { key: "G",   nameAr: "الجبهة (Glabella)" },
  { key: "Pn",  nameAr: "طرف الأنف (Pronasale)" },
  { key: "Cm",  nameAr: "قاعدة العمود الأنفي (Columella)" },
  { key: "Sn",  nameAr: "تحت الأنف (Subnasale)" },
  { key: "Ls",  nameAr: "الشفة العلوية (Labrale superius)" },
  { key: "Li",  nameAr: "الشفة السفلية (Labrale inferius)" },
  { key: "Si",  nameAr: "الثلم الذقني-الشفوي (Labiomental)" },
  { key: "Pog", nameAr: "الذقن الرخو (Pogonion)" },
  { key: "Me",  nameAr: "أسفل الذقن (Menton)" },
];

export interface PhotoNorm {
  normal: number;
  sd: number;
  nameAr: string;
  /** Interpretation when the value is meaningfully ABOVE / BELOW the norm. */
  interpHi: string;
  interpLo: string;
}

export const PHOTO_NORMS: Record<string, PhotoNorm> = {
  FacialConvexity: {
    normal: 12, sd: 4, nameAr: "تحدّب الوجه الرخو (G–Sn–Pog)",
    interpHi: "ملف محدّب — ميل للصنف الثاني الهيكلي",
    interpLo: "ملف مقعّر — ميل للصنف الثالث الهيكلي",
  },
  Nasolabial: {
    normal: 102, sd: 8, nameAr: "الزاوية الأنفية-الشفوية (Cm–Sn–Ls)",
    interpHi: "زاوية منفرجة — ميل القاطع العلوي/الشفة للخلف",
    interpLo: "زاوية حادّة — بروز القاطع العلوي/الشفة للأمام",
  },
  Mentolabial: {
    normal: 120, sd: 10, nameAr: "الزاوية الذقنية-الشفوية (Li–Si–Pog)",
    interpHi: "ثلم ذقني عميق",
    interpLo: "ثلم ذقني ضحل",
  },
};

export type PhotoSeverity = "normal" | "mild" | "severe";

export interface PhotoMeasurement {
  key: string;
  nameAr: string;
  value: number | null;
  normal: number;
  sd: number;
  deviation: number | null;
  severity: PhotoSeverity;
  interpretationAr?: string;
}

function r1(v: number): number { return +v.toFixed(1); }

/** Computes the soft-tissue profile measurements from placed landmarks. */
export function computePhotoMeasurements(pts: Record<string, Pt>): PhotoMeasurement[] {
  const has = (...keys: string[]) => keys.every((k) => pts[k]);
  const out: PhotoMeasurement[] = [];

  const add = (key: string, value: number | null) => {
    const n = PHOTO_NORMS[key];
    const deviation = value !== null ? r1(value - n.normal) : null;
    const ratio = deviation !== null ? Math.abs(deviation / n.sd) : 0;
    const severity: PhotoSeverity =
      deviation === null ? "normal" : ratio <= 1 ? "normal" : ratio <= 2 ? "mild" : "severe";
    let interpretationAr: string | undefined;
    if (deviation !== null) {
      interpretationAr = severity === "normal"
        ? "ضمن الحدود الطبيعية"
        : deviation > 0 ? n.interpHi : n.interpLo;
    }
    out.push({ key, nameAr: n.nameAr, value, normal: n.normal, sd: n.sd, deviation, severity, interpretationAr });
  };

  // Facial convexity (soft tissue, excluding the nose) = 180° − ∠(G, Sn, Pog).
  add("FacialConvexity", has("G", "Sn", "Pog") ? r1(180 - angle3(pts.G, pts.Sn, pts.Pog)) : null);
  // Nasolabial angle = angle at Sn between the columella (Cm) and upper lip (Ls).
  add("Nasolabial", has("Cm", "Sn", "Ls") ? r1(angle3(pts.Cm, pts.Sn, pts.Ls)) : null);
  // Mentolabial angle = angle at the labiomental sulcus (Si) between Li and Pog.
  add("Mentolabial", has("Li", "Si", "Pog") ? r1(angle3(pts.Li, pts.Si, pts.Pog)) : null);

  return out;
}

/** Overall profile classification from the soft-tissue facial convexity. */
export function profileType(measurements: PhotoMeasurement[]): string | null {
  const fc = measurements.find((m) => m.key === "FacialConvexity")?.value;
  if (fc == null) return null;
  if (fc > 16) return "ملف محدّب (Convex) — ميل للصنف الثاني";
  if (fc < 8) return "ملف مقعّر (Concave) — ميل للصنف الثالث";
  return "ملف مستقيم (Straight) — ضمن الطبيعي";
}
