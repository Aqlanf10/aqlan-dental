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
