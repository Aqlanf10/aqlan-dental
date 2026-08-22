export type DailyJourneySummary = {
  patient: DailyJourneyPatient;
  todayAppointment: DailyJourneyAppointment | null;
  queueStatus: DailyJourneyQueue | null;
  todayVisit: DailyJourneyVisit | null;
  financeSummary: DailyJourneyFinance | null;
  unpaidInvoicesCount: number;
  activeContract: DailyJourneyContract | null;
  activeOrthoCase: DailyJourneyOrtho | null;
  medicalAlerts: MedicalAlert[];
  recentVisits: DailyJourneyRecentVisit[];
  timeline: TimelineEvent[];
  journeyStep: string;
  nextAction: string;
};

export type DailyJourneyPatient = {
  id: string;
  patientNumber: string;
  fullName: string;
  phone?: string | null;
  email?: string | null;
  gender?: string | null;
  age?: number | null;
  branchId?: string | null;
  primaryDoctorId?: string | null;
};

export type DailyJourneyAppointment = {
  id: string;
  appointmentDate: string;
  startTime: string;
  endTime?: string | null;
  appointmentType?: string | null;
  status: string;
  doctorId?: string | null;
  doctorName: string;
  serviceId?: string | null;
  roomName?: string | null;
  specialty?: string | null;
  arrivedAt?: string | null;
  calledAt?: string | null;
  inRoomAt?: string | null;
  notes?: string | null;
};

export type DailyJourneyQueue = {
  id: string;
  status: string;
  roomName?: string | null;
  calledAt?: string | null;
  inRoomAt?: string | null;
  startedAt?: string | null;
  doctorId?: string | null;
  serviceId?: string | null;
};

export type DailyJourneyVisit = {
  id: string;
  visitType?: string | null;
  specialty?: string | null;
  doctorId?: string | null;
  chiefComplaint?: string | null;
  clinicalNotes?: string | null;
  treatmentDone?: string | null;
  diagnosis?: string | null;
  instructions?: string | null;
  nextVisitPlan?: string | null;
  cost?: number | null;
  nextVisitDate?: string | null;
  checkoutStatus?: string | null;
  readyForCheckoutAt?: string | null;
  amountDueReference?: number | null;
  appointmentId?: string | null;
};

export type DailyJourneyFinance = {
  totalTreatmentCost?: number | null;
  totalPaid?: number | null;
  outstandingBalance: number;
  overdueAmount: number;
  latestPayment?: {
    id: string;
    amount: number;
    paymentDate: string;
    paymentMethod?: string | null;
    receiptNumber?: string | null;
  } | null;
  financialStatus: string;
  activeContractsCount?: number | null;
  totalPaymentsCount?: number | null;
};

export type DailyJourneyContract = {
  id: string;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  installmentAmount?: number | null;
  installmentsCount: number;
  specialty?: string | null;
  startDate?: string | null;
  status: string;
};

export type DailyJourneyOrtho = {
  id: string;
  caseNumber?: string | null;
  status: string;
  applianceType?: string | null;
  startDate?: string | null;
  expectedDurationMonths?: number | null;
  currentStage?: string | null;
  doctorId?: string | null;
  totalFee?: number | null;
  stagePercentage?: number | null;
};

export type MedicalAlert = {
  type: string;
  label: string;
  value: string;
  severity: "danger" | "warning" | "info" | string;
};

export type DailyJourneyRecentVisit = {
  id: string;
  visitDate: string;
  visitType?: string | null;
  chiefComplaint?: string | null;
  treatmentDone?: string | null;
  diagnosis?: string | null;
  doctorId?: string | null;
  cost?: number | null;
};

export type TimelineEvent = {
  date: string;
  type: string;
  title: string;
  sub: string;
  status?: string | null;
};
