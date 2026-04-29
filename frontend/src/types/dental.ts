export interface ToothCondition {
  id?: string;
  toothNumber: string;
  condition?: string;
  surfacesAffected?: string;
  treatmentDone?: string;
  notes?: string;
}

export interface DentalChart {
  id: string;
  patientId: string;
  chartDate: string;
  doctorName?: string;
  teeth: ToothCondition[];
}

export interface GeneralTreatment {
  id: string;
  patientId: string;
  patientName?: string;
  treatmentType: string;
  toothNumber?: string;
  materialUsed?: string;
  anesthesiaType?: string;
  cost?: number;
  doctorName?: string;
  notes?: string;
  createdAt: string;
}

export interface CreateGeneralTreatmentRequest {
  patientId: string;
  treatmentType: string;
  toothNumber?: string;
  materialUsed?: string;
  anesthesiaType?: string;
  cost?: number;
  doctorId?: string;
  notes?: string;
}

// ─── Periodontal Assessment ───────────────────────────────────────────────────

export interface PerioSite {
  pocketDepth: number; // mm
  bleeding: boolean;
  recession?: number;
}

export interface PerioTooth {
  toothNumber: string;
  sites: {
    mb: PerioSite; // mesiobuccal
    b: PerioSite;  // buccal
    db: PerioSite; // distobuccal
    ml: PerioSite; // mesiolingual
    l: PerioSite;  // lingual
    dl: PerioSite; // distolingual
  };
  mobility: "0" | "I" | "II" | "III";
  furcation: "none" | "I" | "II" | "III";
}

export interface PerioAssessment {
  id: string;
  patientId: string;
  assessmentDate: string;
  doctorId?: string;
  doctorName?: string;
  teeth: PerioTooth[];
  recessionLevel?: string;
  perioStage?: "healthy" | "gingivitis" | "mild" | "moderate" | "severe";
  boneLossPattern?: string;
  attachmentLoss?: string;
  plaqueIndex?: number;
  gingivalIndex?: number;
  avgPocketDepth?: number;
  bleedingPercentage?: number;
  treatmentPlan?: string;
  recommendation?: string;
  createdAt: string;
}

export interface CreatePerioAssessmentRequest {
  patientId: string;
  assessmentDate: string;
  doctorId?: string;
  teeth: PerioTooth[];
  recessionLevel?: string;
  perioStage?: string;
  boneLossPattern?: string;
  attachmentLoss?: string;
  plaqueIndex?: number;
  gingivalIndex?: number;
  treatmentPlan?: string;
  recommendation?: string;
}

export interface UpdatePerioAssessmentRequest extends Partial<CreatePerioAssessmentRequest> {
  id: string;
}
