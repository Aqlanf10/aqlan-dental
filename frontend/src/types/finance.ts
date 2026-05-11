export interface Contract {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  specialty?: string;
  totalAmount: number;
  downPayment: number;
  paidAmount: number;
  remainingAmount: number;
  installmentsCount: number;
  installmentAmount?: number;
  startDate?: string;
  status: string;
  discountAmount?: number;
  discountReason?: string;
  notes?: string;
  payments?: Payment[];
}

export interface Payment {
  id: string;
  patientId: string;
  patientName: string;
  contractId?: string;
  amount: number;
  paymentDate: string;
  paymentMethod?: string;
  serviceDescription?: string;
  specialty?: string;
  doctorId?: string;
  doctorName?: string;
  receiptNumber?: string;
  notes?: string;
  isActive?: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface FinanceSummary {
  todayCollected: number;
  monthCollected: number;
  totalOutstanding: number;
  activeContracts: number;
  recentPayments: Payment[];
}

export interface PatientFinanceSummary {
  totalTreatmentCost: number;
  totalPaid: number;
  outstandingBalance: number;
  overdueAmount: number;
  latestPayment: Payment | null;
  financialStatus: "no_plan" | "paid_full" | "has_balance" | "overdue";
  activeContractsCount: number;
  totalPaymentsCount: number;
}

export interface CreateContractRequest {
  patientId: string;
  specialty?: string;
  relatedCaseId?: string;
  totalAmount: number;
  downPayment?: number;
  installmentsCount?: number;
  installmentAmount?: number;
  startDate?: string;
  discountAmount?: number;
  discountReason?: string;
  notes?: string;
}

export interface CreatePaymentRequest {
  patientId: string;
  contractId?: string;
  amount: number;
  paymentMethod?: string;
  serviceDescription?: string;
  specialty?: string;
  doctorId?: string;
  notes?: string;
}

export interface UpdatePaymentRequest {
  amount?: number;
  paymentDate?: string;
  paymentMethod?: string;
  serviceDescription?: string;
  specialty?: string;
  doctorId?: string;
  notes?: string;
}
