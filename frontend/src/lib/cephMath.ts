// ---------------------------------------------------------------------------
// Cephalometric math — geometry + all analyses
// Re-exports CephMeasurement from types as the single unified type.
// ---------------------------------------------------------------------------

import type { CephMeasurement, MeasurementGroup } from "@/types/ceph";
export type { CephMeasurement };

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
// Normal-value tables
// All norms sourced from published orthodontic literature.
// ---------------------------------------------------------------------------

interface Norm {
  normal: number;
  sd: number;
  unit: '°' | 'mm';
  nameAr: string;
}

export const STEINER_NORMS: Record<string, Norm> = {
  'SNA':          { normal: 82,  sd: 2, unit: '°',  nameAr: 'زاوية SNA' },
  'SNB':          { normal: 80,  sd: 2, unit: '°',  nameAr: 'زاوية SNB' },
  'ANB':          { normal: 2,   sd: 1, unit: '°',  nameAr: 'زاوية ANB' },
  'SND':          { normal: 76,  sd: 2, unit: '°',  nameAr: 'زاوية SND' },
  'U1-NA_angle':  { normal: 22,  sd: 2, unit: '°',  nameAr: 'U1/NA (°)' },
  'U1-NA_mm':     { normal: 4,   sd: 2, unit: 'mm', nameAr: 'U1/NA (mm)' },
  'L1-NB_angle':  { normal: 25,  sd: 2, unit: '°',  nameAr: 'L1/NB (°)' },
  'L1-NB_mm':     { normal: 4,   sd: 2, unit: 'mm', nameAr: 'L1/NB (mm)' },
  'U1-L1':        { normal: 131, sd: 6, unit: '°',  nameAr: 'زاوية القاطعين' },
  'GoGn-SN':      { normal: 32,  sd: 6, unit: '°',  nameAr: 'GoGn / SN' },
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

export const DOWNS_NORMS: Record<string, Norm> = {
  'Convexity':       { normal: 0,    sd: 5, unit: '°', nameAr: 'انحناء الوجه (N-A-Pog)' },
  'AB-FacialPlane':  { normal: -4.6, sd: 3, unit: '°', nameAr: 'خط A-B إلى مستوى الوجه' },
  'Y-Axis':          { normal: 59.4, sd: 4, unit: '°', nameAr: 'محور Y (S-Gn/FH)' },
  'Facial-Plane-FH': { normal: 87.8, sd: 3, unit: '°', nameAr: 'مستوى الوجه (N-Pog) / FH' },
  'Mandibular-FH':   { normal: 21.9, sd: 4, unit: '°', nameAr: 'مستوى الفك السفلي / FH' },
};

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
  norm: Norm,
  group: MeasurementGroup
): CephMeasurement {
  const deviation = value !== null ? r1(value - norm.normal) : null;
  const { severity, direction } = deviation !== null
    ? computeStatus(deviation, norm.sd)
    : { severity: 'normal' as const, direction: 'within' as const };
  const interpretationAr = deviation !== null
    ? arabicInterpretation(name, deviation, severity)
    : undefined;
  return { name, nameAr: norm.nameAr, value, normal: norm.normal, stdDev: norm.sd,
           unit: norm.unit, deviation, severity, direction, analysisGroup: group, interpretationAr };
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
// Build combined list filtered by analysis type
// ---------------------------------------------------------------------------

export function buildMeasurementList(
  lmMap: Record<string, Pt>,
  mmPerPx: number | null,
  groups: readonly MeasurementGroup[] = ['steiner','tweed','mcnamara','ricketts','downs']
): CephMeasurement[] {
  const all: CephMeasurement[] = [];
  if (groups.includes('steiner'))  all.push(...computeSteiner(lmMap, mmPerPx));
  if (groups.includes('tweed'))    all.push(...computeTweed(lmMap));
  if (groups.includes('mcnamara')) all.push(...computeMcNamara(lmMap, mmPerPx));
  if (groups.includes('ricketts')) all.push(...computeRicketts(lmMap, mmPerPx));
  if (groups.includes('downs'))    all.push(...computeDowns(lmMap));
  return all;
}
