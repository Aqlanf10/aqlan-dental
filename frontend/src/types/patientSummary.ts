export interface PatientOrthoSummary {
  caseNumber: string;
  applianceType?: string | null;
  stagePercentage: number;
}

export interface PatientSurgerySummary {
  caseNumber: string;
  surgeryType: string;
  status: string;
}

export interface PatientSummary {
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalPaid: number | null;
  totalOutstanding: number | null;
  unbilledVisitsAmount?: number | null;
  prescriptionsCount: number;
  lastVisitDate?: string | null;
  lastVisitDoctor?: string | null;
  lastVisitDiagnosis?: string | null;
  nextAppointmentDate?: string | null;
  nextAppointmentTime?: string | null;
  nextAppointmentType?: string | null;
  nextAppointmentDoctor?: string | null;
  chiefComplaint?: string | null;
  currentDiagnosis?: string | null;
  nextPlannedStep?: string | null;
  activeOrthoSummary?: PatientOrthoSummary[];
  activeSurgerySummary?: PatientSurgerySummary[];
  medicalAlerts?: string[];
}
