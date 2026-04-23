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
  | 'full';

/** Arabic labels for analysis types. */
export const ANALYSIS_TYPE_AR: Record<AnalysisType, string> = {
  steiner:  'ستاينر',
  tweed:    'تويد',
  mcnamara: 'ماكنامارا',
  ricketts: 'ريكتس',
  downs:    'داونز',
  full:     'شامل',
};

/** Which analyses to run for each AnalysisType. */
export const ANALYSIS_GROUPS: Record<AnalysisType, MeasurementGroup[]> = {
  steiner:  ['steiner'],
  tweed:    ['tweed'],
  mcnamara: ['mcnamara'],
  ricketts: ['ricketts'],
  downs:    ['downs'],
  full:     ['steiner', 'tweed', 'mcnamara', 'ricketts', 'downs'],
};

/** A measurement belongs to exactly one analysis group. */
export type MeasurementGroup =
  | 'steiner'
  | 'tweed'
  | 'mcnamara'
  | 'ricketts'
  | 'downs';

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
  unit: '°' | 'mm';
  /** value − normal. `null` iff `value` is null. */
  deviation: number | null;
  severity: MeasurementSeverity;
  direction: MeasurementDirection;
  analysisGroup: MeasurementGroup;
  interpretationAr?: string;
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
  caseNumber?: string;
  patientName: string;
  analysisType: AnalysisType;
  analysisDate: string;
  xrayFileUrl?: string;
  aiAssisted: boolean;
  /** `null` when the image has not been calibrated yet. */
  pixelsPerMm: number | null;
  imageWidth?: number;
  imageHeight?: number;
  landmarks: CephLandmark[];
  measurements: CephMeasurement[];
  diagnosis?: CephDiagnosis;
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
}
