// ---------------------------------------------------------------------------
// Cephalometric type definitions — canonical source of truth
// ---------------------------------------------------------------------------

/** Supported cephalometric analysis types. */
export type AnalysisType =
  | 'steiner'
  | 'tweed'
  | 'mcnamara'
  | 'ricketts'
  | 'downs'
  | 'jarabak'
  | 'wits'
  | 'full';

/** Arabic labels for analysis types. */
export const ANALYSIS_TYPE_AR: Record<AnalysisType, string> = {
  steiner:  'ستاينر',
  tweed:    'تويد',
  mcnamara: 'ماكنامارا',
  ricketts: 'ريكتس',
  downs:    'داونز',
  jarabak:  'جاراباك',
  wits:     'وتس',
  full:     'شامل',
};

/** Which analyses to run for each AnalysisType. */
export const ANALYSIS_GROUPS: Record<AnalysisType, MeasurementGroup[]> = {
  steiner:  ['steiner'],
  tweed:    ['tweed'],
  mcnamara: ['mcnamara'],
  ricketts: ['ricketts'],
  downs:    ['downs'],
  jarabak:  ['jarabak'],
  wits:     ['wits'],
  full:     ['steiner', 'tweed', 'mcnamara', 'ricketts', 'downs', 'jarabak', 'wits'],
};

/** A measurement belongs to exactly one analysis group. */
export type MeasurementGroup =
  | 'steiner'
  | 'tweed'
  | 'mcnamara'
  | 'ricketts'
  | 'downs'
  | 'jarabak'
  | 'wits';

/** Anatomical region of a landmark — used only for UI grouping. */
export type LandmarkGroup =
  | 'cranial'
  | 'maxilla'
  | 'mandible'
  | 'dental'
  | 'soft';

/** Severity of deviation from the norm (magnitude only). */
export type MeasurementSeverity = 'normal' | 'mild' | 'severe';

/** Direction of deviation relative to the norm. */
export type MeasurementDirection = 'above' | 'below' | 'within';

export interface CephLandmark {
  id?: string;
  key: string;
  name: string;
  nameAr: string;
  x: number;
  y: number;
  isAiPlaced: boolean;
  confidence?: number;
  /**
   * Optional short note (Arabic or English) explaining WHY the AI placed this
   * landmark at this position. Surfaced in the canvas tooltip and the side
   * panel so the orthodontist can read the model's reasoning before accepting
   * or moving the point. Not persisted on save.
   */
  reasoning?: string;
  /** Present when the landmark was created with a LANDMARK_DEFS entry; may be
   *  absent on records loaded from older rows. Consumers should fall back to a
   *  lookup table when they need the group. */
  group?: LandmarkGroup;
}

/**
 * Unified measurement record — used by both the math layer and the UI.
 * Keep in sync with the backend `CephMeasurementDto`.
 */
export interface CephMeasurement {
  name: string;
  nameAr: string;
  value: number | null;
  normal: number;
  stdDev: number;
  unit: '°' | 'mm' | '%';
  /** value − normal. `null` iff `value` is null. */
  deviation: number | null;
  severity: MeasurementSeverity;
  direction: MeasurementDirection;
  analysisGroup: MeasurementGroup;
  interpretationAr?: string;
  /** Interpretation text sourced from a fetched API norm (below/above range). */
  apiInterpretation?: string;
}

/**
 * Norm record returned by `GET /api/ceph-norms` (camelCase).
 * Overlays the built-in norm tables via `applyNormOverrides` in cephMath;
 * built-ins remain the fallback when a field/record is absent.
 */
export interface ApiNorm {
  measurementName: string;
  analysisGroup?: string;
  normalValue: number;
  stdDeviation: number;
  minNormal?: number | null;
  maxNormal?: number | null;
  unit?: string;
  nameAr?: string;
  interpretationBelow?: string | null;
  interpretationNormal?: string | null;
  interpretationAbove?: string | null;
}

/**
 * Response of POST /api/ceph/{id}/ai/draft-diagnosis (C-D).
 * The draft is NEVER auto-saved — it must be explicitly copied by the doctor
 * into the FinalDiagnosis field and saved via the existing diagnosis flow.
 */
export interface CephAiDraftResponse {
  draft: string;
  modelId: string;
  /** Mandatory Arabic review disclaimer — always shown prominently. */
  disclaimer: string;
  generatedAt: string;
}

export interface CephDiagnosis {
  skeletalClass?: string;
  verticalPattern?: string;
  incisorInclination?: string;
  softTissueSummary?: string;
  aiRecommendation?: string;
  doctorApproved: boolean;
  finalDiagnosis?: string;
}

export interface CephAnalysis {
  id: string;
  orthoCaseId: string;
  patientId: string;
  caseNumber?: string;
  patientName: string;
  analysisType: AnalysisType;
  analysisDate: string;
  xrayFileUrl?: string;
  aiAssisted: boolean;
  /** True when the saved analysis originated from the automatic tracing flow. */
  isAutoTraced?: boolean;
  /** Doctor assigned to the analysis, when the backend has one. */
  doctorId?: string | null;
  /** Clinical notes entered when the analysis was created. */
  notes?: string | null;
  /** `null` when the image has not been calibrated yet. */
  pixelsPerMm: number | null;
  imageWidth?: number;
  imageHeight?: number;
  landmarks: CephLandmark[];
  measurements: CephMeasurement[];
  diagnosis?: CephDiagnosis;
  // ── Clinical approval gate (CEPH-EPIC) ─────────────────────────────────────
  // The final PDF report is blocked until `isApproved` is true.
  isApproved: boolean;
  approvedByUserId?: string | null;
  approvedByName?: string | null;
  approvedAt?: string | null;
  approvalNotes?: string | null;
}

// ---------------------------------------------------------------------------
// Comparison (GET /api/ceph/compare?baseId=&targetId=) — camelCase DTOs
// ---------------------------------------------------------------------------

/** Lightweight analysis info embedded in a comparison result. */
export interface CephCompareAnalysisInfo {
  id: string;
  analysisDate: string;
  analysisType: AnalysisType;
}

/** One measurement row in a before/after comparison. */
export interface CephCompareRow {
  measurementName: string;
  nameAr: string;
  /** `null` when the measurement has no known analysis group. */
  analysisGroup: MeasurementGroup | null;
  unit: string;
  baseValue: number | null;
  targetValue: number | null;
  /** targetValue − baseValue (signed). `null` when either side is missing. */
  delta: number | null;
  normalValue: number;
  stdDeviation: number;
  baseClassification: MeasurementSeverity | null;
  targetClassification: MeasurementSeverity | null;
  /** `true` = moved toward the norm, `false` = away, `null` = not comparable. */
  improved: boolean | null;
}

/** Full response of the comparison endpoint. */
export interface CephCompareResult {
  base: CephCompareAnalysisInfo;
  target: CephCompareAnalysisInfo;
  patientName: string;
  rows: CephCompareRow[];
}

export interface CephAnalysisList {
  id: string;
  orthoCaseId: string;
  caseNumber?: string;
  patientName: string;
  analysisType: AnalysisType;
  analysisDate: string;
  xrayFileUrl?: string;
  aiAssisted: boolean;
  landmarkCount: number;
  hasMeasurements: boolean;
  /** Creation timestamp (ISO) — tiebreaker for selecting the latest analysis,
   *  matching the deck generator (analysisDate DESC, then createdAt DESC). */
  createdAt: string;
}

// ---------------------------------------------------------------------------
// CEPH-EPIC batch C-B — analysis VERSION snapshots
// POST   /api/ceph/{id}/versions              → save current state as a snapshot
// GET    /api/ceph/{id}/versions              → list snapshots (id, label, date)
// GET    /api/ceph/{id}/versions/{versionId}  → full snapshot for compare
// ---------------------------------------------------------------------------

/** List item for GET /api/ceph/{id}/versions (no JSON blobs). */
export interface CephVersionListItem {
  id: string;
  cephAnalysisId: string;
  label: string;
  snapshotDate: string;
  createdAt: string;
}

/**
 * Full snapshot for GET /api/ceph/{id}/versions/{versionId}. Mirrors the live
 * analysis detail DTO but with immutable JSON-backed landmark/measurement/
 * diagnosis payloads captured at save time.
 */
export interface CephVersionDetail {
  id: string;
  cephAnalysisId: string;
  label: string;
  snapshotDate: string;
  createdAt: string;
  landmarks: CephLandmark[];
  measurements: CephMeasurement[];
  diagnosis?: CephDiagnosis;
}
