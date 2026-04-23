export interface CephLandmark {
  id?: string;
  key: string;      // e.g. "S", "N", "A"
  name: string;     // English name
  nameAr: string;   // Arabic name
  x: number;        // pixels in image coordinate space
  y: number;
  isAiPlaced: boolean;
  confidence?: number;  // 0-1
  group: 'cranial' | 'maxilla' | 'mandible' | 'dental' | 'soft';
}

export interface CephMeasurement {
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
  analysisType: string;
  analysisDate: string;
  xrayFileUrl?: string;
  aiAssisted: boolean;
  pixelsPerMm: number;
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
  analysisType: string;
  analysisDate: string;
  xrayFileUrl?: string;
  aiAssisted: boolean;
  landmarkCount: number;
  hasMeasurements: boolean;
}
