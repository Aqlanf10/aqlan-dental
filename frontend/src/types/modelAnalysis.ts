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
