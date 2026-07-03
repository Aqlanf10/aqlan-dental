export interface HuckabaToothInput {
  toothCode: string;
  radiographicUneruptedWidth: number;
  actualReferenceWidth: number;
  radiographicReferenceWidth: number;
}

export interface DentalModelAnalysisInput {
  toothWidths: Record<string, number | null>;
  upperAvailableSpace: number | null;
  lowerAvailableSpace: number | null;
  upperInterpremolarWidth: number | null;
  upperIntermolarWidth: number | null;
  howePremolarDiameter: number | null;
  howePremolarBasalArchWidth: number | null;
  howeBasalArchLength: number | null;
  mixedUpperAvailablePerSide: number | null;
  mixedLowerAvailablePerSide: number | null;
  moyersPercentile: number;
  huckabaTeeth: HuckabaToothInput[];
}

export interface BoltonResult {
  overallRatio: number;
  anteriorRatio: number;
  overallDiscrepancy: number;
  anteriorDiscrepancy: number;
  overallInterpretation: string;
  anteriorInterpretation: string;
}

export interface ArchSpaceResult {
  required: number;
  available: number;
  discrepancy: number;
  interpretation: string;
}

export interface PontResult {
  incisorSum: number;
  predictedInterpremolarWidth: number;
  predictedIntermolarWidth: number;
  measuredInterpremolarWidth: number | null;
  measuredIntermolarWidth: number | null;
  premolarDifference: number | null;
  molarDifference: number | null;
}

export interface HoweResult {
  totalToothMaterial: number;
  premolarDiameterPercent: number;
  premolarBasalArchWidthPercent: number;
  basalArchLength: number | null;
  interpretation: string;
}

export interface MixedDentitionPrediction {
  predictedUpperPerSide: number;
  predictedLowerPerSide: number;
  upperSpaceDiscrepancyPerSide: number | null;
  lowerSpaceDiscrepancyPerSide: number | null;
}

export interface DentalModelAnalysisResult {
  bolton: BoltonResult | null;
  upperArch: ArchSpaceResult | null;
  lowerArch: ArchSpaceResult | null;
  pont: PontResult | null;
  howe: HoweResult | null;
  moyers: {
    percentile: number;
    lowerIncisorSum: number;
    prediction: MixedDentitionPrediction;
  } | null;
  tanakaJohnston: {
    lowerIncisorSum: number;
    prediction: MixedDentitionPrediction;
  } | null;
  huckaba: { toothCode: string; predictedActualWidth: number }[];
  warnings: string[];
}

// ─── QA-599: New analyses ported from the Aqlan Ortho Model Analysis Android app ──

export interface ArchPerimeterResult {
  spaceAvailable: number;
  spaceRequired: number;
  discrepancy: number;
  diagnosis: string;
  comment: string;
}

export interface AshleyHoweResult {
  basalArchPercent: number;
  interpretation: string;
  expansionPossibility: string;
}

export interface LinderHarthResult {
  incisorSum: number;
  predictedInterpremolarWidth: number;
  predictedIntermolarWidth: number;
  measuredInterpremolarWidth: number | null;
  measuredIntermolarWidth: number | null;
  premolarDifference: number | null;
  molarDifference: number | null;
  premolarDiagnosis: string;
  molarDiagnosis: string;
}

export interface PeckPeckToothResult {
  toothName: string;
  mdWidth: number;
  flWidth: number;
  index: number;
  diagnosis: string;
}

export interface PeckPeckResult {
  teeth: PeckPeckToothResult[];
}

export interface KorkhausResult {
  incisorSum: number;
  predictedArchLength: number;
  measuredArchLength: number | null;
  difference: number | null;
  diagnosis: string;
}

export interface NanceMixedResult {
  maxAvailable: number | null;
  maxRequired: number | null;
  maxDiscrepancy: number | null;
  maxDiagnosis: string;
  mandAvailable: number | null;
  mandRequired: number | null;
  mandDiscrepancy: number | null;
  mandDiagnosis: string;
}

/** QA-599: Extended result including all new analyses. */
export interface DentalModelAnalysisResultExtended {
  base: DentalModelAnalysisResult;
  archPerimeter: ArchPerimeterResult | null;
  careys: ArchPerimeterResult | null;
  ashleyHowe: AshleyHoweResult | null;
  linderHarth: LinderHarthResult | null;
  peckPeck: PeckPeckResult | null;
  korkhaus: KorkhausResult | null;
  nanceMixed: NanceMixedResult | null;
}

/** QA-599: New input fields for the extended analyses. */
export interface ExtendedAnalysisInput {
  ashleyHoweTtm?: number | null;
  ashleyHowePmd?: number | null;
  ashleyHowePmbaw?: number | null;
  linderHarthSi?: number | null;
  linderHarthMeasuredPmv?: number | null;
  linderHarthMeasuredMv?: number | null;
  peckMd31?: number | null;
  peckFl31?: number | null;
  peckMd32?: number | null;
  peckFl32?: number | null;
  peckMd41?: number | null;
  peckFl41?: number | null;
  peckMd42?: number | null;
  peckFl42?: number | null;
  korkhausSi?: number | null;
  korkhausMeasuredLength?: number | null;
  nanceMaxAvailable?: number | null;
  nanceMaxRequired?: number | null;
  nanceMandAvailable?: number | null;
  nanceMandRequired?: number | null;
  archPerimeterAvailable?: number | null;
  archPerimeterRequired?: number | null;
  careysAvailable?: number | null;
  careysRequired?: number | null;
}

export interface DentalModelAnalysisRecord {
  id: string;
  orthoCaseId: string;
  analysisDate: string;
  dentitionStage: "Permanent" | "Mixed";
  analysisVersion: string;
  inputs: DentalModelAnalysisInput;
  results: DentalModelAnalysisResult;
  approvedBy?: string | null;
  approvedAt?: string | null;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}
