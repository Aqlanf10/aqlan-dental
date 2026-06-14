// ---------------------------------------------------------------------------
// Cephalometric math — geometry + all analyses
// Re-exports CephMeasurement from types as the single unified type.
// ---------------------------------------------------------------------------

import type { ApiNorm, CephMeasurement, MeasurementGroup } from "@/types/ceph";
export type { CephMeasurement };
export type { ApiNorm };

type Pt = { x: number; y: number };

// ---------------------------------------------------------------------------
// Basic geometry helpers
// ---------------------------------------------------------------------------

export function dist(a: Pt, b: Pt): number {
  const dx = b.x - a.x, dy = b.y - a.y;
  return Math.sqrt(dx * dx + dy * dy);
}

/** Angle at `vertex` between rays vertex→p1 and vertex→p3, in [0, 180]°. */
export function angle3(p1: Pt, vertex: Pt, p3: Pt): number {
  const ax = p1.x - vertex.x, ay = p1.y - vertex.y;
  const bx = p3.x - vertex.x, by = p3.y - vertex.y;
  const dot = ax * bx + ay * by;
  const mag = Math.sqrt((ax*ax+ay*ay) * (bx*bx+by*by));
  if (mag === 0) return 0;
  return (Math.acos(Math.max(-1, Math.min(1, dot / mag))) * 180) / Math.PI;
}

/** Acute angle (0–90°) between two lines. */
export function angleBetweenLines(l1p1: Pt, l1p2: Pt, l2p1: Pt, l2p2: Pt): number {
  const d1x = l1p2.x-l1p1.x, d1y = l1p2.y-l1p1.y;
  const d2x = l2p2.x-l2p1.x, d2y = l2p2.y-l2p1.y;
  const mag1 = Math.sqrt(d1x*d1x+d1y*d1y);
  const mag2 = Math.sqrt(d2x*d2x+d2y*d2y);
  if (mag1 === 0 || mag2 === 0) return 0;
  const cos = Math.abs((d1x*d2x + d1y*d2y) / (mag1*mag2));
  return (Math.acos(Math.max(-1, Math.min(1, cos))) * 180) / Math.PI;
}

/** Unsigned perpendicular distance from pt to line through lp1→lp2. */
export function perpDist(pt: Pt, lp1: Pt, lp2: Pt): number {
  return Math.abs(signedPerpDist(pt, lp1, lp2));
}

/** Signed perpendicular distance — positive = pt is to the right of lp1→lp2. */
export function signedPerpDist(pt: Pt, lp1: Pt, lp2: Pt): number {
  const dx = lp2.x - lp1.x, dy = lp2.y - lp1.y;
  const len = Math.sqrt(dx*dx + dy*dy);
  if (len === 0) return 0;
  return ((pt.x - lp1.x) * dy - (pt.y - lp1.y) * dx) / len;
}

/** Line angle p1→p2 from positive x-axis, range (-180, 180]. */
export function lineAngle(p1: Pt, p2: Pt): number {
  return (Math.atan2(p2.y - p1.y, p2.x - p1.x) * 180) / Math.PI;
}

// ---------------------------------------------------------------------------
// Similarity transform (translation + rotation + uniform scale)
// Used for cephalometric superimposition: two tracings are registered by
// mapping a pair of source landmarks onto the matching pair on the reference
// tracing. Two point-pairs determine the transform exactly.
// ---------------------------------------------------------------------------

/** p' = (a·x − b·y + tx, b·x + a·y + ty); (a,b) encode scale·rotation. */
export interface Similarity { a: number; b: number; tx: number; ty: number; }

/** Identity transform (used as a safe fallback for degenerate input). */
export const IDENTITY_SIMILARITY: Similarity = { a: 1, b: 0, tx: 0, ty: 0 };

/**
 * The similarity that maps srcA→dstA and srcB→dstB exactly. When the two
 * source points coincide (no orientation/scale info) it degrades to a pure
 * translation aligning srcA onto dstA.
 */
export function similarityFromPairs(srcA: Pt, srcB: Pt, dstA: Pt, dstB: Pt): Similarity {
  const sx = srcB.x - srcA.x, sy = srcB.y - srcA.y;
  const srcLen2 = sx * sx + sy * sy;
  if (srcLen2 < 1e-9) {
    return { a: 1, b: 0, tx: dstA.x - srcA.x, ty: dstA.y - srcA.y };
  }
  const dx = dstB.x - dstA.x, dy = dstB.y - dstA.y;
  const a = (sx * dx + sy * dy) / srcLen2;
  const b = (sx * dy - sy * dx) / srcLen2;
  const tx = dstA.x - (a * srcA.x - b * srcA.y);
  const ty = dstA.y - (b * srcA.x + a * srcA.y);
  return { a, b, tx, ty };
}

export function applySimilarity(t: Similarity, p: Pt): Pt {
  return { x: t.a * p.x - t.b * p.y + t.tx, y: t.b * p.x + t.a * p.y + t.ty };
}

// ---------------------------------------------------------------------------
// Normal-value tables
// All norms sourced from published orthodontic literature.
// ---------------------------------------------------------------------------

interface Norm {
  normal: number;
  sd: number;
  unit: '°' | 'mm' | '%';
  nameAr: string;
}

export const STEINER_NORMS: Record<string, Norm> = {
  // ── Skeletal ──
  'SNA':          { normal: 82,  sd: 2, unit: '°',  nameAr: 'زاوية SNA' },
  'SNB':          { normal: 80,  sd: 2, unit: '°',  nameAr: 'زاوية SNB' },
  'ANB':          { normal: 2,   sd: 1, unit: '°',  nameAr: 'زاوية ANB' },
  'SND':          { normal: 76,  sd: 2, unit: '°',  nameAr: 'زاوية SND' },
  // ── Dental ──
  'U1-NA_angle':  { normal: 22,  sd: 2, unit: '°',  nameAr: 'U1/NA (°)' },
  'U1-NA_mm':     { normal: 4,   sd: 2, unit: 'mm', nameAr: 'U1/NA (mm)' },
  'L1-NB_angle':  { normal: 25,  sd: 2, unit: '°',  nameAr: 'L1/NB (°)' },
  'L1-NB_mm':     { normal: 4,   sd: 2, unit: 'mm', nameAr: 'L1/NB (mm)' },
  'U1-L1':        { normal: 131, sd: 6, unit: '°',  nameAr: 'زاوية القاطعين' },
  'GoGn-SN':      { normal: 32,  sd: 6, unit: '°',  nameAr: 'GoGn / SN' },
  // ── Soft tissue (S-line: Pog → midpoint of nose contour) ──
  'UL-SLine':     { normal: 0,   sd: 1, unit: 'mm', nameAr: 'الشفة العلوية — خط S' },
  'LL-SLine':     { normal: 0,   sd: 1, unit: 'mm', nameAr: 'الشفة السفلية — خط S' },
};

export const TWEED_NORMS: Record<string, Norm> = {
  'FMA':  { normal: 25, sd: 4, unit: '°', nameAr: 'FMA (فرانكفورت-فك سفلي)' },
  'FMIA': { normal: 65, sd: 5, unit: '°', nameAr: 'FMIA (فرانكفورت-قاطعة سفلية)' },
  'IMPA': { normal: 90, sd: 4, unit: '°', nameAr: 'IMPA (فك سفلي-قاطعة سفلية)' },
};

export const MCNAMARA_NORMS: Record<string, Norm> = {
  'Co-A':   { normal: 91,  sd: 6, unit: 'mm', nameAr: 'Co-A (طول الفك العلوي)' },
  'Co-Gn':  { normal: 120, sd: 7, unit: 'mm', nameAr: 'Co-Gn (طول الفك السفلي)' },
  'ANS-Me': { normal: 65,  sd: 5, unit: 'mm', nameAr: 'ANS-Me (ارتفاع الوجه السفلي)' },
};

export const RICKETTS_NORMS: Record<string, Norm> = {
  'Facial-Depth':    { normal: 87,  sd: 3, unit: '°',  nameAr: 'عمق الوجه (FH-N-Pog)' },
  'Facial-Axis':     { normal: 90,  sd: 3, unit: '°',  nameAr: 'محور الوجه (Pt-Gn / BaN)' },
  'Mandibular-Plane':{ normal: 26,  sd: 4, unit: '°',  nameAr: 'ميل مستوى الفك (FH-GoMe)' },
  'Convexity-A':     { normal: 2,   sd: 2, unit: 'mm', nameAr: 'انحناء النقطة A (N-Pog)' },
  'L1-APog_mm':      { normal: 1,   sd: 2, unit: 'mm', nameAr: 'L1 إلى خط A-Pog (mm)' },
  'L1-APog_angle':   { normal: 22,  sd: 4, unit: '°',  nameAr: 'L1 إلى خط A-Pog (°)' },
  'Upper-Lip-ELine': { normal: -2,  sd: 2, unit: 'mm', nameAr: 'الشفة العلوية إلى خط E' },
  'Lower-Lip-ELine': { normal: -2,  sd: 2, unit: 'mm', nameAr: 'الشفة السفلية إلى خط E' },
  'Nasolabial':      { normal: 102, sd: 8, unit: '°',  nameAr: 'الزاوية الأنفية-الشفوية' },
};

export const WITS_NORMS: Record<string, Norm> = {
  // Wits appraisal: AO-BO distance on the functional occlusal plane.
  // Male norm: -1mm, Female norm: 0mm → using combined 0 ± 1.5mm.
  // Positive = Class II tendency, Negative = Class III tendency.
  'Wits': { normal: 0, sd: 1.5, unit: 'mm', nameAr: 'مسافة وتس (AO-BO)' },
};

export const DOWNS_NORMS: Record<string, Norm> = {
  'Convexity':       { normal: 0,    sd: 5, unit: '°', nameAr: 'انحناء الوجه (N-A-Pog)' },
  'AB-FacialPlane':  { normal: -4.6, sd: 3, unit: '°', nameAr: 'خط A-B إلى مستوى الوجه' },
  'Y-Axis':          { normal: 59.4, sd: 4, unit: '°', nameAr: 'محور Y (S-Gn/FH)' },
  'Facial-Plane-FH': { normal: 87.8, sd: 3, unit: '°', nameAr: 'مستوى الوجه (N-Pog) / FH' },
  'Mandibular-FH':   { normal: 21.9, sd: 4, unit: '°', nameAr: 'مستوى الفك السفلي / FH' },
};

// Jarabak (Björk) analysis: the four-angle polygon (saddle/articular/gonial +
// their sum) describes the cranial base and mandibular form, while the
// posterior/anterior facial-height ratio predicts the growth direction.
// Norms from Jarabak & Fizzell. The ratio is a pure length ratio (S-Go / N-Me)
// so it needs no calibration.
export const JARABAK_NORMS: Record<string, Norm> = {
  'Saddle-Angle':    { normal: 123, sd: 5, unit: '°', nameAr: 'الزاوية السرجية (N-S-Ar)' },
  'Articular-Angle': { normal: 143, sd: 6, unit: '°', nameAr: 'الزاوية المفصلية (S-Ar-Go)' },
  'Gonial-Angle':    { normal: 130, sd: 7, unit: '°', nameAr: 'الزاوية الفكية (Ar-Go-Me)' },
  'Bjork-Sum':       { normal: 396, sd: 6, unit: '°', nameAr: 'مجموع زوايا بيورك' },
  'Jarabak-Ratio':   { normal: 64,  sd: 4, unit: '%', nameAr: 'نسبة جاراباك (الارتفاع الخلفي/الأمامي)' },
};

// ---------------------------------------------------------------------------
// API norm overrides
// Fetched from GET /api/ceph-norms and overlaid on the built-in tables above.
// Built-ins always remain the fallback: a measurement without an override —
// or with an invalid one — keeps its published norm.
// ---------------------------------------------------------------------------

let normOverrides: Record<string, ApiNorm> = {};

/**
 * Replace the active norm-override set. Calling with `[]` (or invalid data)
 * restores built-ins for everything. Invalid entries (missing name,
 * non-finite values, non-positive SD) are ignored.
 */
export function applyNormOverrides(norms: ApiNorm[]): void {
  const next: Record<string, ApiNorm> = {};
  if (Array.isArray(norms)) {
    for (const n of norms) {
      if (!n || typeof n.measurementName !== 'string' || !n.measurementName) continue;
      if (!Number.isFinite(n.normalValue) || !Number.isFinite(n.stdDeviation)) continue;
      if (n.stdDeviation <= 0) continue;
      next[n.measurementName] = n;
    }
  }
  normOverrides = next;
}

/** Effective norm for `name`: API override if present, otherwise the built-in. */
function resolveNorm(name: string, builtin: Norm): { norm: Norm; override: ApiNorm | null } {
  const ov = normOverrides[name];
  if (!ov) return { norm: builtin, override: null };
  return {
    norm: {
      normal: ov.normalValue,
      sd: ov.stdDeviation,
      unit: ov.unit === 'mm' || ov.unit === '°' || ov.unit === '%' ? ov.unit : builtin.unit,
      nameAr: ov.nameAr && ov.nameAr.trim() ? ov.nameAr : builtin.nameAr,
    },
    override: ov,
  };
}

// ---------------------------------------------------------------------------
// Status computation (severity + direction)
// ---------------------------------------------------------------------------

function computeStatus(
  deviation: number,
  sd: number
): Pick<CephMeasurement, 'severity' | 'direction'> {
  const direction = deviation > 0 ? 'above' : deviation < 0 ? 'below' : 'within';
  const ratio = Math.abs(deviation / sd);
  const severity = ratio <= 1 ? 'normal' : ratio <= 2 ? 'mild' : 'severe';
  return { severity, direction };
}

// ---------------------------------------------------------------------------
// Arabic interpretation per measurement
// ---------------------------------------------------------------------------

function arabicInterpretation(
  name: string,
  deviation: number,
  severity: CephMeasurement['severity']
): string {
  if (severity === 'normal') return 'ضمن الحدود الطبيعية';
  const s = severity === 'severe' ? 'بشكل واضح' : 'بشكل طفيف';
  const above = deviation > 0;

  const map: Record<string, [string, string]> = {
    'SNA':          ['الفك العلوي بارز للأمام', 'الفك العلوي متراجع للخلف'],
    'SNB':          ['الفك السفلي بارز للأمام', 'الفك السفلي متراجع للخلف'],
    'ANB':          ['علاقة هيكلية من الدرجة الثانية', 'علاقة هيكلية من الدرجة الثالثة'],
    'SND':          ['SND مرتفع', 'SND منخفض'],
    'U1-NA_angle':  ['القاطع العلوي مائل للأمام', 'القاطع العلوي مائل للخلف'],
    'U1-NA_mm':     ['القاطع العلوي بارز', 'القاطع العلوي مرتد'],
    'L1-NB_angle':  ['القاطع السفلي مائل للأمام', 'القاطع السفلي مائل للخلف'],
    'L1-NB_mm':     ['القاطع السفلي بارز', 'القاطع السفلي مرتد'],
    'U1-L1':        ['زاوية القاطعين مفتوحة (قواطع مرتدة)', 'زاوية القاطعين ضيقة (بروز سني)'],
    'GoGn-SN':      ['نمط رأسي مرتفع (وجه طويل)', 'نمط رأسي منخفض (وجه قصير)'],
    'FMA':          ['ميل الفك السفلي مرتفع — نمط رأسي', 'ميل الفك السفلي منخفض — نمط أفقي'],
    'FMIA':         ['القاطع السفلي مائل للخلف نسبة لـFH', 'القاطع السفلي بارز للأمام نسبة لـFH'],
    'IMPA':         ['القاطع السفلي منتصب بشكل زائد', 'القاطع السفلي مائل للخلف'],
    'Co-A':         ['طول الفك العلوي أكبر من المعدل', 'طول الفك العلوي أقل من المعدل'],
    'Co-Gn':        ['طول الفك السفلي أكبر من المعدل', 'طول الفك السفلي أقل من المعدل'],
    'ANS-Me':       ['ارتفاع الوجه السفلي مرتفع', 'ارتفاع الوجه السفلي منخفض'],
    'Facial-Depth': ['الوجه أكثر بروزاً من المعدل', 'الوجه أكثر تراجعاً من المعدل'],
    'Facial-Axis':  ['محور نمو أمامي متزايد', 'محور نمو خلفي متزايد'],
    'Mandibular-Plane': ['مستوى الفك السفلي مائل بشكل مرتفع', 'مستوى الفك السفلي مسطّح'],
    'Convexity-A':  ['بروز عظمي واضح (Class II هيكلي)', 'تراجع عظمي (Class III هيكلي)'],
    'L1-APog_mm':   ['القاطع السفلي بارز عن خط A-Pog', 'القاطع السفلي مرتد عن خط A-Pog'],
    'L1-APog_angle':['القاطع السفلي مائل للأمام', 'القاطع السفلي مائل للخلف'],
    'Upper-Lip-ELine': ['الشفة العلوية بارزة للأمام', 'الشفة العلوية مرتدة للخلف'],
    'Lower-Lip-ELine': ['الشفة السفلية بارزة للأمام', 'الشفة السفلية مرتدة للخلف'],
    'Nasolabial':   ['الزاوية الأنفية مفتوحة (ميل شفوي)','الزاوية الأنفية ضيقة (بروز شفوي)'],
    'Convexity':    ['بروز هيكلي للوجه', 'تراجع هيكلي للوجه'],
    'AB-FacialPlane':['الفك السفلي بارز', 'الفك السفلي متراجع'],
    'Y-Axis':       ['نمط نمو رأسي', 'نمط نمو أفقي'],
    'Facial-Plane-FH': ['مستوى الوجه بزاوية أعلى', 'مستوى الوجه بزاوية أقل'],
    'Mandibular-FH':['مستوى الفك السفلي مرتفع', 'مستوى الفك السفلي منخفض'],
    'UL-SLine':     ['الشفة العلوية بارزة أمام خط S', 'الشفة العلوية مرتدة خلف خط S'],
    'LL-SLine':     ['الشفة السفلية بارزة أمام خط S', 'الشفة السفلية مرتدة خلف خط S'],
    'Wits':         ['تنافر هيكلي من الصنف الثاني (AO أمام BO)', 'تنافر هيكلي من الصنف الثالث (BO أمام AO)'],
    'Saddle-Angle':    ['الزاوية السرجية مفتوحة (وضع خلفي للحفرة الفكية)', 'الزاوية السرجية ضيقة (وضع أمامي للحفرة الفكية)'],
    'Articular-Angle': ['الزاوية المفصلية مفتوحة — ميل لنمط عمودي', 'الزاوية المفصلية ضيقة — ميل لنمط أفقي'],
    'Gonial-Angle':    ['زاوية الفك مفتوحة — نمط نمو عمودي وميل لعضة مفتوحة', 'زاوية الفك ضيقة — نمط نمو أفقي وميل لعضة عميقة'],
    'Bjork-Sum':       ['مجموع الزوايا مرتفع — نمط نمو عمودي (دوران خلفي للفك)', 'مجموع الزوايا منخفض — نمط نمو أفقي (دوران أمامي للفك)'],
    'Jarabak-Ratio':   ['نسبة عالية — اتجاه نمو أفقي (دوران أمامي مضاد لعقارب الساعة)', 'نسبة منخفضة — اتجاه نمو عمودي (دوران خلفي مع عقارب الساعة)'],
  };

  const [hi, lo] = map[name] ?? ['أعلى من المعدل', 'أقل من المعدل'];
  return `${above ? hi : lo} ${s}`;
}

// ---------------------------------------------------------------------------
// Core builder
// ---------------------------------------------------------------------------

function r1(v: number): number { return +v.toFixed(1); }

function makeMeasurement(
  name: string,
  value: number | null,
  builtinNorm: Norm,
  group: MeasurementGroup
): CephMeasurement {
  const { norm, override } = resolveNorm(name, builtinNorm);
  const deviation = value !== null ? r1(value - norm.normal) : null;
  const { severity, direction } = deviation !== null
    ? computeStatus(deviation, norm.sd)
    : { severity: 'normal' as const, direction: 'within' as const };
  const interpretationAr = deviation !== null
    ? arabicInterpretation(name, deviation, severity)
    : undefined;
  // Interpretation strings from the API norm — only when the value falls
  // outside the [minNormal, maxNormal] range (defaults: normal ± sd).
  let apiInterpretation: string | undefined;
  if (override && value !== null) {
    const min = override.minNormal ?? norm.normal - norm.sd;
    const max = override.maxNormal ?? norm.normal + norm.sd;
    if (value < min)      apiInterpretation = override.interpretationBelow ?? undefined;
    else if (value > max) apiInterpretation = override.interpretationAbove ?? undefined;
  }
  return { name, nameAr: norm.nameAr, value, normal: norm.normal, stdDev: norm.sd,
           unit: norm.unit, deviation, severity, direction, analysisGroup: group,
           interpretationAr, apiInterpretation };
}

// ---------------------------------------------------------------------------
// Steiner analysis
// ---------------------------------------------------------------------------

export function computeSteiner(lmMap: Record<string, Pt>, mmPerPx: number | null): CephMeasurement[] {
  const cal = mmPerPx !== null && mmPerPx > 0;
  const G = (k: string) => lmMap[k];
  const has = (...keys: string[]) => keys.every(k => lmMap[k]);
  const r: CephMeasurement[] = [];
  const add = (n: string, v: number | null) => r.push(makeMeasurement(n, v, STEINER_NORMS[n], 'steiner'));

  add('SNA',        has('S','N','A') ? r1(angle3(G('S'),G('N'),G('A'))) : null);
  add('SNB',        has('S','N','B') ? r1(angle3(G('S'),G('N'),G('B'))) : null);
  const sna = r.find(m=>m.name==='SNA')?.value, snb = r.find(m=>m.name==='SNB')?.value;
  add('ANB',        sna!=null && snb!=null ? r1(sna-snb) : null);
  add('SND',        has('S','N','D') ? r1(angle3(G('S'),G('N'),G('D'))) : null);
  add('U1-NA_angle',has('U1A','U1T','N','A') ? r1(angleBetweenLines(G('U1A'),G('U1T'),G('N'),G('A'))) : null);
  add('U1-NA_mm',   cal && has('U1T','N','A') ? r1(signedPerpDist(G('U1T'),G('N'),G('A'))*(mmPerPx!)) : null);
  add('L1-NB_angle',has('L1A','L1T','N','B') ? r1(angleBetweenLines(G('L1A'),G('L1T'),G('N'),G('B'))) : null);
  add('L1-NB_mm',   cal && has('L1T','N','B') ? r1(signedPerpDist(G('L1T'),G('N'),G('B'))*(mmPerPx!)) : null);
  add('U1-L1',      has('U1A','U1T','L1A','L1T') ? r1(180-angleBetweenLines(G('U1A'),G('U1T'),G('L1A'),G('L1T'))) : null);
  add('GoGn-SN',    has('Go','Me','S','N') ? r1(angleBetweenLines(G('Go'),G('Me'),G('S'),G('N'))) : null);

  // ── Soft tissue (Steiner S-line: Pog → midpoint between Cm and Pn) ──
  // The S-line reference runs from soft-tissue Pogonion (approximated here by
  // hard-tissue Pog) to the midpoint of the nose contour. Lips should touch
  // the line; positive = lip anterior to S-line, negative = posterior.
  const sLineMid = has('Cm','Pn')
    ? { x: (G('Cm').x + G('Pn').x) / 2, y: (G('Cm').y + G('Pn').y) / 2 }
    : null;
  add('UL-SLine', cal && sLineMid && has('LS','Pog')
    ? r1(signedPerpDist(G('LS'), G('Pog'), sLineMid) * mmPerPx!) : null);
  add('LL-SLine', cal && sLineMid && has('LI','Pog')
    ? r1(signedPerpDist(G('LI'), G('Pog'), sLineMid) * mmPerPx!) : null);
  return r;
}

// ---------------------------------------------------------------------------
// Wits appraisal
// ---------------------------------------------------------------------------
//
// Classic Wits uses the functional occlusal plane defined by molar contacts
// and incisor edges. Without posterior occlusal landmarks in our set, we
// approximate the occlusal plane as the line from the midpoint of the
// incisor edges (U1T, L1T) to the midpoint of the mandibular posterior
// region (Go, Me). Points A and B are projected perpendicular onto this
// plane; Wits = signed distance AO − BO along the plane (mm).
// Positive → Class II tendency, Negative → Class III tendency.
// ---------------------------------------------------------------------------

export function computeWits(lmMap: Record<string, Pt>, mmPerPx: number | null): CephMeasurement[] {
  const cal = mmPerPx !== null && mmPerPx > 0;
  const has = (...keys: string[]) => keys.every(k => lmMap[k]);
  const r: CephMeasurement[] = [];
  const add = (n: string, v: number | null) => r.push(makeMeasurement(n, v, WITS_NORMS[n], 'wits'));

  if (!cal || !has('U1T','L1T','Go','Me','A','B')) {
    add('Wits', null);
    return r;
  }

  // Occlusal plane approximation
  const pA: Pt = {
    x: (lmMap['U1T'].x + lmMap['L1T'].x) / 2,
    y: (lmMap['U1T'].y + lmMap['L1T'].y) / 2,
  };
  const pB: Pt = {
    x: (lmMap['Go'].x + lmMap['Me'].x) / 2,
    y: (lmMap['Go'].y + lmMap['Me'].y) / 2,
  };

  // Perpendicular foot of point P onto line pA→pB
  const project = (p: Pt): Pt => {
    const dx = pB.x - pA.x, dy = pB.y - pA.y;
    const len2 = dx*dx + dy*dy;
    if (len2 === 0) return pA;
    const t = ((p.x - pA.x) * dx + (p.y - pA.y) * dy) / len2;
    return { x: pA.x + t * dx, y: pA.y + t * dy };
  };

  const AO = project(lmMap['A']);
  const BO = project(lmMap['B']);

  // Signed distance along plane direction (pA → pB)
  const dirX = pB.x - pA.x, dirY = pB.y - pA.y;
  const dLen = Math.sqrt(dirX*dirX + dirY*dirY);
  if (dLen === 0) { add('Wits', null); return r; }
  const ux = dirX / dLen, uy = dirY / dLen;
  // Wits = signed scalar projection of (BO - AO) onto plane direction.
  // Convention: positive when AO is anterior to BO (Class II).
  const signed = ((AO.x - BO.x) * ux + (AO.y - BO.y) * uy) * mmPerPx!;
  add('Wits', r1(signed));
  return r;
}

// ---------------------------------------------------------------------------
// Tweed analysis
// ---------------------------------------------------------------------------

export function computeTweed(lmMap: Record<string, Pt>): CephMeasurement[] {
  const G = (k: string) => lmMap[k];
  const has = (...keys: string[]) => keys.every(k => lmMap[k]);
  const r: CephMeasurement[] = [];
  const add = (n: string, v: number | null) => r.push(makeMeasurement(n, v, TWEED_NORMS[n], 'tweed'));

  add('FMA',  has('Po','Or','Go','Me') ? r1(angleBetweenLines(G('Po'),G('Or'),G('Go'),G('Me'))) : null);
  add('FMIA', has('Po','Or','L1A','L1T') ? r1(angleBetweenLines(G('Po'),G('Or'),G('L1A'),G('L1T'))) : null);
  add('IMPA', has('Go','Me','L1A','L1T') ? r1(angleBetweenLines(G('Go'),G('Me'),G('L1A'),G('L1T'))) : null);
  return r;
}

// ---------------------------------------------------------------------------
// McNamara analysis
// ---------------------------------------------------------------------------

export function computeMcNamara(lmMap: Record<string, Pt>, mmPerPx: number | null): CephMeasurement[] {
  const cal = mmPerPx !== null && mmPerPx > 0;
  const G = (k: string) => lmMap[k];
  const has = (...keys: string[]) => keys.every(k => lmMap[k]);
  const r: CephMeasurement[] = [];
  const add = (n: string, v: number | null) => r.push(makeMeasurement(n, v, MCNAMARA_NORMS[n], 'mcnamara'));

  add('Co-A',   cal && has('Co','A')   ? r1(dist(G('Co'),G('A'))  * mmPerPx!) : null);
  add('Co-Gn',  cal && has('Co','Gn')  ? r1(dist(G('Co'),G('Gn')) * mmPerPx!) : null);
  add('ANS-Me', cal && has('ANS','Me') ? r1(dist(G('ANS'),G('Me'))* mmPerPx!) : null);
  return r;
}

// ---------------------------------------------------------------------------
// Ricketts analysis
// ---------------------------------------------------------------------------

export function computeRicketts(lmMap: Record<string, Pt>, mmPerPx: number | null): CephMeasurement[] {
  const cal = mmPerPx !== null && mmPerPx > 0;
  const G = (k: string) => lmMap[k];
  const has = (...keys: string[]) => keys.every(k => lmMap[k]);
  const r: CephMeasurement[] = [];
  const add = (n: string, v: number | null) => r.push(makeMeasurement(n, v, RICKETTS_NORMS[n], 'ricketts'));

  // Facial Depth: angle FH to N-Pog (how far Pog is forward — higher = more protrusive)
  add('Facial-Depth',
    has('Po','Or','N','Pog') ? r1(angleBetweenLines(G('Po'),G('Or'),G('N'),G('Pog'))) : null);

  // Facial Axis: Pt-Gn line to BaN line (Ba not in set — approximate as S-N to Gn axis)
  add('Facial-Axis',
    has('S','N','Gn') ? r1(angleBetweenLines(G('S'),G('N'),G('N'),G('Gn'))) : null);

  // Mandibular Plane to FH
  add('Mandibular-Plane',
    has('Po','Or','Go','Me') ? r1(angleBetweenLines(G('Po'),G('Or'),G('Go'),G('Me'))) : null);

  // Convexity of A: signed dist of A from N-Pog plane (mm)
  add('Convexity-A',
    cal && has('N','A','Pog')
      ? r1(signedPerpDist(G('A'), G('N'), G('Pog')) * mmPerPx!)
      : null);

  // Lower incisor to A-Pog (mm)
  add('L1-APog_mm',
    cal && has('L1T','A','Pog')
      ? r1(signedPerpDist(G('L1T'), G('A'), G('Pog')) * mmPerPx!)
      : null);

  // Lower incisor to A-Pog (angle)
  add('L1-APog_angle',
    has('L1A','L1T','A','Pog')
      ? r1(angleBetweenLines(G('L1A'),G('L1T'),G('A'),G('Pog')))
      : null);

  // Upper lip to E-plane: signed dist of LS from Pn-Pog line (mm)
  add('Upper-Lip-ELine',
    cal && has('LS','Pn','Pog')
      ? r1(signedPerpDist(G('LS'), G('Pn'), G('Pog')) * mmPerPx!)
      : null);

  // Lower lip to E-plane
  add('Lower-Lip-ELine',
    cal && has('LI','Pn','Pog')
      ? r1(signedPerpDist(G('LI'), G('Pn'), G('Pog')) * mmPerPx!)
      : null);

  // Nasolabial angle: Pn-Cm-LS
  add('Nasolabial',
    has('Pn','Cm','LS') ? r1(angle3(G('Pn'),G('Cm'),G('LS'))) : null);

  return r;
}

// ---------------------------------------------------------------------------
// Downs analysis
// ---------------------------------------------------------------------------

export function computeDowns(lmMap: Record<string, Pt>): CephMeasurement[] {
  const G = (k: string) => lmMap[k];
  const has = (...keys: string[]) => keys.every(k => lmMap[k]);
  const r: CephMeasurement[] = [];
  const add = (n: string, v: number | null) => r.push(makeMeasurement(n, v, DOWNS_NORMS[n], 'downs'));

  // Angle of convexity: angle N-A-Pog (positive = convex profile)
  add('Convexity',
    has('N','A','Pog') ? r1(angle3(G('N'),G('A'),G('Pog')) - 180) : null);

  // A-B plane to facial plane (N-Pog)
  add('AB-FacialPlane',
    has('A','B','N','Pog') ? r1(angleBetweenLines(G('A'),G('B'),G('N'),G('Pog'))) * -1 : null);

  // Y-axis: S-Gn angle to FH
  add('Y-Axis',
    has('S','Gn','Po','Or') ? r1(angleBetweenLines(G('S'),G('Gn'),G('Po'),G('Or'))) : null);

  // Facial plane (N-Pog) to FH
  add('Facial-Plane-FH',
    has('N','Pog','Po','Or') ? r1(angleBetweenLines(G('N'),G('Pog'),G('Po'),G('Or'))) : null);

  // Mandibular plane to FH
  add('Mandibular-FH',
    has('Go','Me','Po','Or') ? r1(angleBetweenLines(G('Go'),G('Me'),G('Po'),G('Or'))) : null);

  return r;
}

// ---------------------------------------------------------------------------
// Jarabak (Björk) analysis
// ---------------------------------------------------------------------------
//
// The Björk polygon: saddle (N-S-Ar), articular (S-Ar-Go) and gonial
// (Ar-Go-Me) angles, plus their sum (≈396° — higher = backward/vertical
// rotation). The Jarabak ratio is posterior facial height (S-Go) over
// anterior facial height (N-Me) as a percentage; ≥65% predicts a horizontal
// (counter-clockwise) growth direction, <59% a vertical (clockwise) one.
// All four angles and the ratio are scale-independent — no calibration needed.
// ---------------------------------------------------------------------------

export function computeJarabak(lmMap: Record<string, Pt>): CephMeasurement[] {
  const G = (k: string) => lmMap[k];
  const has = (...keys: string[]) => keys.every(k => lmMap[k]);
  const r: CephMeasurement[] = [];
  const add = (n: string, v: number | null) => r.push(makeMeasurement(n, v, JARABAK_NORMS[n], 'jarabak'));

  const saddle    = has('N','S','Ar')  ? r1(angle3(G('N'),  G('S'),  G('Ar'))) : null;
  const articular = has('S','Ar','Go') ? r1(angle3(G('S'),  G('Ar'), G('Go'))) : null;
  const gonial    = has('Ar','Go','Me')? r1(angle3(G('Ar'), G('Go'), G('Me'))) : null;

  add('Saddle-Angle',    saddle);
  add('Articular-Angle', articular);
  add('Gonial-Angle',    gonial);
  add('Bjork-Sum',
    saddle !== null && articular !== null && gonial !== null
      ? r1(saddle + articular + gonial) : null);

  // Posterior/anterior facial height ratio — a ratio of pixel distances, so it
  // is valid even on an uncalibrated image.
  add('Jarabak-Ratio',
    has('S','Go','N','Me') && dist(G('N'),G('Me')) > 0
      ? r1((dist(G('S'),G('Go')) / dist(G('N'),G('Me'))) * 100) : null);

  return r;
}

// ---------------------------------------------------------------------------
// Build combined list filtered by analysis type
// ---------------------------------------------------------------------------

export function buildMeasurementList(
  lmMap: Record<string, Pt>,
  mmPerPx: number | null,
  groups: readonly MeasurementGroup[] = ['steiner','tweed','mcnamara','ricketts','downs','jarabak','wits']
): CephMeasurement[] {
  const all: CephMeasurement[] = [];
  if (groups.includes('steiner'))  all.push(...computeSteiner(lmMap, mmPerPx));
  if (groups.includes('tweed'))    all.push(...computeTweed(lmMap));
  if (groups.includes('mcnamara')) all.push(...computeMcNamara(lmMap, mmPerPx));
  if (groups.includes('ricketts')) all.push(...computeRicketts(lmMap, mmPerPx));
  if (groups.includes('downs'))    all.push(...computeDowns(lmMap));
  if (groups.includes('jarabak'))  all.push(...computeJarabak(lmMap));
  if (groups.includes('wits'))     all.push(...computeWits(lmMap, mmPerPx));
  return all;
}
