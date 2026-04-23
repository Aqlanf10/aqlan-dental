// ---------------------------------------------------------------------------
// Cephalometric math utilities
// ---------------------------------------------------------------------------

type Pt = { x: number; y: number };

// ---------------------------------------------------------------------------
// Basic geometry
// ---------------------------------------------------------------------------

/**
 * Euclidean distance between two points.
 */
export function dist(a: Pt, b: Pt): number {
  const dx = b.x - a.x;
  const dy = b.y - a.y;
  return Math.sqrt(dx * dx + dy * dy);
}

/**
 * Angle at `vertex` formed by the rays vertex→p1 and vertex→p3.
 * Returns a value in the range [0, 180] degrees.
 */
export function angle3(p1: Pt, vertex: Pt, p3: Pt): number {
  const v1x = p1.x - vertex.x;
  const v1y = p1.y - vertex.y;
  const v2x = p3.x - vertex.x;
  const v2y = p3.y - vertex.y;

  const dot = v1x * v2x + v1y * v2y;
  const mag1 = Math.sqrt(v1x * v1x + v1y * v1y);
  const mag2 = Math.sqrt(v2x * v2x + v2y * v2y);

  if (mag1 === 0 || mag2 === 0) return 0;

  const cosTheta = Math.max(-1, Math.min(1, dot / (mag1 * mag2)));
  return (Math.acos(cosTheta) * 180) / Math.PI;
}

/**
 * Acute angle (0–90°) between two lines defined by two points each.
 */
export function angleBetweenLines(
  l1p1: Pt,
  l1p2: Pt,
  l2p1: Pt,
  l2p2: Pt
): number {
  const d1x = l1p2.x - l1p1.x;
  const d1y = l1p2.y - l1p1.y;
  const d2x = l2p2.x - l2p1.x;
  const d2y = l2p2.y - l2p1.y;

  const mag1 = Math.sqrt(d1x * d1x + d1y * d1y);
  const mag2 = Math.sqrt(d2x * d2x + d2y * d2y);

  if (mag1 === 0 || mag2 === 0) return 0;

  const cosTheta = Math.abs((d1x * d2x + d1y * d2y) / (mag1 * mag2));
  const clampedCos = Math.max(-1, Math.min(1, cosTheta));
  return (Math.acos(clampedCos) * 180) / Math.PI;
}

/**
 * Unsigned perpendicular distance from `pt` to the infinite line through
 * `lp1` and `lp2`.
 */
export function perpDist(pt: Pt, lp1: Pt, lp2: Pt): number {
  return Math.abs(signedPerpDist(pt, lp1, lp2));
}

/**
 * Signed perpendicular distance from `pt` to the infinite line through
 * `lp1` → `lp2`.
 *
 * Sign convention: positive = the point is to the *right* of the direction
 * vector lp1→lp2 (i.e. the "anterior" side in a standard lateral cephalogram
 * where the line is directed superiorly, e.g. N→A or N→B).
 */
export function signedPerpDist(pt: Pt, lp1: Pt, lp2: Pt): number {
  const dx = lp2.x - lp1.x;
  const dy = lp2.y - lp1.y;
  const len = Math.sqrt(dx * dx + dy * dy);
  if (len === 0) return 0;
  // Cross product (lp1→lp2) × (lp1→pt), divided by length
  return ((pt.x - lp1.x) * dy - (pt.y - lp1.y) * dx) / len;
}

/**
 * Angle of the line p1→p2 measured from the positive x-axis, in degrees.
 * Range: (-180, 180].
 */
export function lineAngle(p1: Pt, p2: Pt): number {
  return (Math.atan2(p2.y - p1.y, p2.x - p1.x) * 180) / Math.PI;
}

// ---------------------------------------------------------------------------
// Measurement result types
// ---------------------------------------------------------------------------

export interface MeasurementResult {
  name: string;
  nameAr: string;
  value: number | null;
  normal: number;
  stdDev: number;
  unit: '°' | 'mm';
  deviation: number | null;
  status: 'normal' | 'mild' | 'severe';
  analysisGroup: 'steiner' | 'tweed' | 'mcnamara';
  interpretationAr?: string;
}

// ---------------------------------------------------------------------------
// Computed measurement interfaces
// ---------------------------------------------------------------------------

export interface SteinerMeasurements {
  SNA: number | null;
  SNB: number | null;
  ANB: number | null;
  SND: number | null;
  'U1-NA_angle': number | null;
  'U1-NA_mm': number | null;
  'L1-NB_angle': number | null;
  'L1-NB_mm': number | null;
  'U1-L1': number | null;
  'GoGn-SN': number | null;
}

export interface TweedMeasurements {
  FMA: number | null;
  FMIA: number | null;
  IMPA: number | null;
}

export interface McNamaraMeasurements {
  'Co-A': number | null;   // in mm
  'Co-Gn': number | null;  // in mm
  'ANS-Me': number | null; // lower face height in mm
}

// ---------------------------------------------------------------------------
// Normal value tables
// ---------------------------------------------------------------------------

export const STEINER_NORMS: Record<
  string,
  { normal: number; sd: number; unit: '°' | 'mm'; nameAr: string }
> = {
  SNA:          { normal: 82,  sd: 2, unit: '°',  nameAr: 'SNA' },
  SNB:          { normal: 80,  sd: 2, unit: '°',  nameAr: 'SNB' },
  ANB:          { normal: 2,   sd: 1, unit: '°',  nameAr: 'ANB' },
  SND:          { normal: 76,  sd: 2, unit: '°',  nameAr: 'SND' },
  'U1-NA_angle':{ normal: 22,  sd: 2, unit: '°',  nameAr: 'U1/NA (°)' },
  'U1-NA_mm':   { normal: 4,   sd: 2, unit: 'mm', nameAr: 'U1/NA (mm)' },
  'L1-NB_angle':{ normal: 25,  sd: 2, unit: '°',  nameAr: 'L1/NB (°)' },
  'L1-NB_mm':   { normal: 4,   sd: 2, unit: 'mm', nameAr: 'L1/NB (mm)' },
  'U1-L1':      { normal: 131, sd: 6, unit: '°',  nameAr: 'زاوية القاطعين' },
  'GoGn-SN':    { normal: 32,  sd: 6, unit: '°',  nameAr: 'GoGn / SN' },
};

export const TWEED_NORMS: Record<
  string,
  { normal: number; sd: number; unit: '°' | 'mm'; nameAr: string }
> = {
  FMA:  { normal: 25, sd: 4, unit: '°', nameAr: 'FMA' },
  FMIA: { normal: 65, sd: 5, unit: '°', nameAr: 'FMIA' },
  IMPA: { normal: 90, sd: 4, unit: '°', nameAr: 'IMPA' },
};

export const MCNAMARA_NORMS: Record<
  string,
  { normal: number; sd: number; unit: '°' | 'mm'; nameAr: string }
> = {
  'Co-A':   { normal: 91,  sd: 6, unit: 'mm', nameAr: 'Co-A' },
  'Co-Gn':  { normal: 120, sd: 7, unit: 'mm', nameAr: 'Co-Gn' },
  'ANS-Me': { normal: 65,  sd: 5, unit: 'mm', nameAr: 'ANS-Me' },
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Return the point from the record, or undefined if the key is absent. */
function lm(record: Record<string, Pt>, key: string): Pt | undefined {
  return record[key];
}

/**
 * Round a number to one decimal place using standard arithmetic rounding.
 * Using `+value.toFixed(1)` as specified.
 */
function round1(value: number): number {
  return +value.toFixed(1);
}

// ---------------------------------------------------------------------------
// Steiner analysis
// ---------------------------------------------------------------------------

export function computeSteiner(
  lmMap: Record<string, Pt>,
  mmPerPx: number
): SteinerMeasurements {
  const calibrated = mmPerPx > 0;

  // SNA: angle at N between S and A
  const SNA: number | null =
    lm(lmMap, 'S') && lm(lmMap, 'N') && lm(lmMap, 'A')
      ? round1(angle3(lmMap['S'], lmMap['N'], lmMap['A']))
      : null;

  // SNB: angle at N between S and B
  const SNB: number | null =
    lm(lmMap, 'S') && lm(lmMap, 'N') && lm(lmMap, 'B')
      ? round1(angle3(lmMap['S'], lmMap['N'], lmMap['B']))
      : null;

  // ANB: SNA - SNB (signed)
  const ANB: number | null =
    SNA !== null && SNB !== null ? round1(SNA - SNB) : null;

  // SND: angle at N between S and D
  const SND: number | null =
    lm(lmMap, 'S') && lm(lmMap, 'N') && lm(lmMap, 'D')
      ? round1(angle3(lmMap['S'], lmMap['N'], lmMap['D']))
      : null;

  // U1-NA angle: angle between upper incisor axis and N-A line
  const U1_NA_angle: number | null =
    lm(lmMap, 'U1A') && lm(lmMap, 'U1T') && lm(lmMap, 'N') && lm(lmMap, 'A')
      ? round1(
          angleBetweenLines(
            lmMap['U1A'],
            lmMap['U1T'],
            lmMap['N'],
            lmMap['A']
          )
        )
      : null;

  // U1-NA mm: distance of upper incisor tip from N-A line
  const U1_NA_mm: number | null =
    calibrated && lm(lmMap, 'U1T') && lm(lmMap, 'N') && lm(lmMap, 'A')
      ? round1(signedPerpDist(lmMap['U1T'], lmMap['N'], lmMap['A']) * mmPerPx)
      : null;

  // L1-NB angle: angle between lower incisor axis and N-B line
  const L1_NB_angle: number | null =
    lm(lmMap, 'L1A') && lm(lmMap, 'L1T') && lm(lmMap, 'N') && lm(lmMap, 'B')
      ? round1(
          angleBetweenLines(
            lmMap['L1A'],
            lmMap['L1T'],
            lmMap['N'],
            lmMap['B']
          )
        )
      : null;

  // L1-NB mm: distance of lower incisor tip from N-B line
  const L1_NB_mm: number | null =
    calibrated && lm(lmMap, 'L1T') && lm(lmMap, 'N') && lm(lmMap, 'B')
      ? round1(signedPerpDist(lmMap['L1T'], lmMap['N'], lmMap['B']) * mmPerPx)
      : null;

  // U1-L1: interincisal angle = 180 - angle between incisor axes
  const U1_L1: number | null =
    lm(lmMap, 'U1A') &&
    lm(lmMap, 'U1T') &&
    lm(lmMap, 'L1A') &&
    lm(lmMap, 'L1T')
      ? round1(
          180 -
            angleBetweenLines(
              lmMap['U1A'],
              lmMap['U1T'],
              lmMap['L1A'],
              lmMap['L1T']
            )
        )
      : null;

  // GoGn-SN: mandibular plane (Go→Me) to S-N plane
  const GoGn_SN: number | null =
    lm(lmMap, 'Go') &&
    lm(lmMap, 'Me') &&
    lm(lmMap, 'S') &&
    lm(lmMap, 'N')
      ? round1(
          angleBetweenLines(
            lmMap['Go'],
            lmMap['Me'],
            lmMap['S'],
            lmMap['N']
          )
        )
      : null;

  return {
    SNA,
    SNB,
    ANB,
    SND,
    'U1-NA_angle': U1_NA_angle,
    'U1-NA_mm': U1_NA_mm,
    'L1-NB_angle': L1_NB_angle,
    'L1-NB_mm': L1_NB_mm,
    'U1-L1': U1_L1,
    'GoGn-SN': GoGn_SN,
  };
}

// ---------------------------------------------------------------------------
// Tweed analysis
// ---------------------------------------------------------------------------

export function computeTweed(
  lmMap: Record<string, Pt>,
  mmPerPx: number
): TweedMeasurements {
  // mmPerPx unused for Tweed (all angular), included for API consistency

  // FMA: FH plane (Po→Or) to mandibular plane (Go→Me)
  const FMA: number | null =
    lm(lmMap, 'Po') &&
    lm(lmMap, 'Or') &&
    lm(lmMap, 'Go') &&
    lm(lmMap, 'Me')
      ? round1(
          angleBetweenLines(
            lmMap['Po'],
            lmMap['Or'],
            lmMap['Go'],
            lmMap['Me']
          )
        )
      : null;

  // FMIA: FH plane (Po→Or) to lower incisor axis (L1A→L1T)
  const FMIA: number | null =
    lm(lmMap, 'Po') &&
    lm(lmMap, 'Or') &&
    lm(lmMap, 'L1A') &&
    lm(lmMap, 'L1T')
      ? round1(
          angleBetweenLines(
            lmMap['Po'],
            lmMap['Or'],
            lmMap['L1A'],
            lmMap['L1T']
          )
        )
      : null;

  // IMPA: mandibular plane (Go→Me) to lower incisor axis (L1A→L1T)
  const IMPA: number | null =
    lm(lmMap, 'Go') &&
    lm(lmMap, 'Me') &&
    lm(lmMap, 'L1A') &&
    lm(lmMap, 'L1T')
      ? round1(
          angleBetweenLines(
            lmMap['Go'],
            lmMap['Me'],
            lmMap['L1A'],
            lmMap['L1T']
          )
        )
      : null;

  // Suppress unused-variable warning for mmPerPx in strict mode
  void mmPerPx;

  return { FMA, FMIA, IMPA };
}

// ---------------------------------------------------------------------------
// McNamara analysis
// ---------------------------------------------------------------------------

export function computeMcNamara(
  lmMap: Record<string, Pt>,
  mmPerPx: number
): McNamaraMeasurements {
  const calibrated = mmPerPx > 0;

  // Co-A: condylion to point A
  const Co_A: number | null =
    calibrated && lm(lmMap, 'Co') && lm(lmMap, 'A')
      ? round1(dist(lmMap['Co'], lmMap['A']) * mmPerPx)
      : null;

  // Co-Gn: condylion to gnathion
  const Co_Gn: number | null =
    calibrated && lm(lmMap, 'Co') && lm(lmMap, 'Gn')
      ? round1(dist(lmMap['Co'], lmMap['Gn']) * mmPerPx)
      : null;

  // ANS-Me: anterior nasal spine to menton (lower face height)
  const ANS_Me: number | null =
    calibrated && lm(lmMap, 'ANS') && lm(lmMap, 'Me')
      ? round1(dist(lmMap['ANS'], lmMap['Me']) * mmPerPx)
      : null;

  return {
    'Co-A': Co_A,
    'Co-Gn': Co_Gn,
    'ANS-Me': ANS_Me,
  };
}

// ---------------------------------------------------------------------------
// Arabic interpretation helpers
// ---------------------------------------------------------------------------

/**
 * Compute a deviation-based status:
 *  - |dev/sd| <= 1 → 'normal'
 *  - |dev/sd| <= 2 → 'mild'
 *  - |dev/sd| >  2 → 'severe'
 */
function computeStatus(
  deviation: number,
  sd: number
): 'normal' | 'mild' | 'severe' {
  const ratio = Math.abs(deviation / sd);
  if (ratio <= 1) return 'normal';
  if (ratio <= 2) return 'mild';
  return 'severe';
}

/**
 * Generate a simple Arabic interpretation string based on the measurement
 * name, its deviation, and the unit.
 */
function arabicInterpretation(
  name: string,
  deviation: number,
  unit: '°' | 'mm',
  status: 'normal' | 'mild' | 'severe'
): string {
  if (status === 'normal') return 'ضمن الحدود الطبيعية';

  const severity = status === 'severe' ? 'بشكل واضح' : 'بشكل طفيف';
  const direction = deviation > 0 ? 'أعلى من المعدل الطبيعي' : 'أقل من المعدل الطبيعي';
  const unitLabel = unit === '°' ? 'درجة' : 'ملم';

  // Measurement-specific interpretations
  switch (name) {
    case 'SNA':
      return deviation > 0
        ? `الفك العلوي بارز للأمام ${severity} (${direction})`
        : `الفك العلوي مندرج للخلف ${severity} (${direction})`;
    case 'SNB':
      return deviation > 0
        ? `الفك السفلي بارز للأمام ${severity} (${direction})`
        : `الفك السفلي مندرج للخلف ${severity} (${direction})`;
    case 'ANB':
      return deviation > 0
        ? `علاقة هيكلية من الدرجة الثانية ${severity}`
        : `علاقة هيكلية من الدرجة الثالثة ${severity}`;
    case 'SND':
      return `قيمة SND ${direction} ${severity}`;
    case 'U1-NA_angle':
    case 'U1-NA_mm':
      return deviation > 0
        ? `القاطع العلوي مائل للأمام ${severity}`
        : `القاطع العلوي مائل للخلف ${severity}`;
    case 'L1-NB_angle':
    case 'L1-NB_mm':
      return deviation > 0
        ? `القاطع السفلي مائل للأمام ${severity}`
        : `القاطع السفلي مائل للخلف ${severity}`;
    case 'U1-L1':
      return deviation > 0
        ? `زاوية القاطعين مفتوحة ${severity} (قواطع مائلة للخلف)`
        : `زاوية القاطعين مغلقة ${severity} (قواطع بارزة)`;
    case 'GoGn-SN':
      return deviation > 0
        ? `نمط رأسي مفتوح (وجه طويل) ${severity}`
        : `نمط رأسي مغلق (وجه قصير) ${severity}`;
    case 'FMA':
      return deviation > 0
        ? `ميل مستوى الفك السفلي مرتفع ${severity} (نمط رأسي)`
        : `ميل مستوى الفك السفلي منخفض ${severity} (نمط أفقي)`;
    case 'FMIA':
      return deviation > 0
        ? `القاطع السفلي مائل للخلف بالنسبة لمستوى فرانكفورت ${severity}`
        : `القاطع السفلي مائل للأمام بالنسبة لمستوى فرانكفورت ${severity}`;
    case 'IMPA':
      return deviation > 0
        ? `القاطع السفلي منتصب بشكل زائد ${severity}`
        : `القاطع السفلي مائل للخلف ${severity}`;
    case 'Co-A':
      return deviation > 0
        ? `طول الفك العلوي أكبر من المعدل ${severity}`
        : `طول الفك العلوي أقل من المعدل ${severity}`;
    case 'Co-Gn':
      return deviation > 0
        ? `طول الفك السفلي أكبر من المعدل ${severity}`
        : `طول الفك السفلي أقل من المعدل ${severity}`;
    case 'ANS-Me':
      return deviation > 0
        ? `ارتفاع الوجه السفلي مرتفع ${severity}`
        : `ارتفاع الوجه السفلي منخفض ${severity}`;
    default:
      return `القيمة ${direction} بمقدار ${Math.abs(+deviation.toFixed(1))} ${unitLabel}`;
  }
}

// ---------------------------------------------------------------------------
// Build combined measurement list
// ---------------------------------------------------------------------------

export function buildMeasurementList(
  steiner: SteinerMeasurements,
  tweed: TweedMeasurements,
  mcnamara: McNamaraMeasurements,
  calibrated: boolean
): MeasurementResult[] {
  const results: MeasurementResult[] = [];

  // Helper to add a single measurement
  function addMeasurement(
    name: string,
    value: number | null,
    norms: Record<string, { normal: number; sd: number; unit: '°' | 'mm'; nameAr: string }>,
    group: 'steiner' | 'tweed' | 'mcnamara'
  ): void {
    const norm = norms[name];
    if (!norm) return;

    // For uncalibrated mm measurements value will be null — still add the row
    const deviation: number | null =
      value !== null ? round1(value - norm.normal) : null;

    const status: 'normal' | 'mild' | 'severe' =
      deviation !== null ? computeStatus(deviation, norm.sd) : 'normal';

    const interpretationAr: string | undefined =
      deviation !== null
        ? arabicInterpretation(name, deviation, norm.unit, status)
        : undefined;

    results.push({
      name,
      nameAr: norm.nameAr,
      value,
      normal: norm.normal,
      stdDev: norm.sd,
      unit: norm.unit,
      deviation,
      status,
      analysisGroup: group,
      interpretationAr,
    });
  }

  // Steiner measurements
  const steinerEntries: Array<[keyof SteinerMeasurements, string]> = [
    ['SNA',          'SNA'],
    ['SNB',          'SNB'],
    ['ANB',          'ANB'],
    ['SND',          'SND'],
    ['U1-NA_angle',  'U1-NA_angle'],
    ['U1-NA_mm',     'U1-NA_mm'],
    ['L1-NB_angle',  'L1-NB_angle'],
    ['L1-NB_mm',     'L1-NB_mm'],
    ['U1-L1',        'U1-L1'],
    ['GoGn-SN',      'GoGn-SN'],
  ];
  for (const [key, normKey] of steinerEntries) {
    // Skip mm rows entirely when not calibrated (no landmarks contributed either)
    const isMm = STEINER_NORMS[normKey]?.unit === 'mm';
    if (isMm && !calibrated) {
      // Still add the row with null value so the UI can show it as uncalibrated
      addMeasurement(normKey, null, STEINER_NORMS, 'steiner');
    } else {
      addMeasurement(normKey, steiner[key], STEINER_NORMS, 'steiner');
    }
  }

  // Tweed measurements (all angular, no calibration dependency)
  const tweedEntries: Array<[keyof TweedMeasurements, string]> = [
    ['FMA',  'FMA'],
    ['FMIA', 'FMIA'],
    ['IMPA', 'IMPA'],
  ];
  for (const [key, normKey] of tweedEntries) {
    addMeasurement(normKey, tweed[key], TWEED_NORMS, 'tweed');
  }

  // McNamara measurements (all linear)
  const mcnamaraEntries: Array<[keyof McNamaraMeasurements, string]> = [
    ['Co-A',   'Co-A'],
    ['Co-Gn',  'Co-Gn'],
    ['ANS-Me', 'ANS-Me'],
  ];
  for (const [key, normKey] of mcnamaraEntries) {
    if (!calibrated) {
      addMeasurement(normKey, null, MCNAMARA_NORMS, 'mcnamara');
    } else {
      addMeasurement(normKey, mcnamara[key], MCNAMARA_NORMS, 'mcnamara');
    }
  }

  return results;
}
