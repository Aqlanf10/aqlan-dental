export interface DrugItem {
  name: string;
  dose: string;
  frequency: string;
  duration: string;
  notes?: string;
}

export interface PrescriptionListItem {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName?: string;
  diagnosis?: string;
  drugCount: number;
  notes?: string;
  createdAt: string;
}

export interface Prescription {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName?: string;
  diagnosis?: string;
  drugs: DrugItem[];
  notes?: string;
  createdAt: string;
}

export interface CreatePrescriptionRequest {
  patientId: string;
  doctorId?: string;
  diagnosis?: string;
  drugs: DrugItem[];
  notes?: string;
}
