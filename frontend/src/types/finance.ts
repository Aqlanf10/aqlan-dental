// ─── Finance Types ──────────────────────────────────────────────────────────

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
  createdAt?: string;
  updatedAt?: string;
}

export interface Payment {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber?: string;
  contractId?: string;
  amount: number;
  paymentDate: string;
  paymentMethod?: string;
  serviceDescription?: string;
  specialty?: string;
  doctorName?: string;
  receiptNumber?: string;
  notes?: string;
  createdAt?: string;
}

export interface FinanceSummary {
  todayCollected: number;
  monthCollected: number;
  totalOutstanding: number;
  activeContracts: number;
  recentPayments: Payment[];
  // Enhanced fields
  averageContractValue?: number;
  collectionRate?: number;
  overduePercentage?: number;
  overdueContractsCount?: number;
  totalContractsValue?: number;
  totalPaidValue?: number;
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

export interface UpdateContractRequest {
  totalAmount?: number;
  downPayment?: number;
  installmentsCount?: number;
  installmentAmount?: number;
  startDate?: string;
  discountAmount?: number;
  discountReason?: string;
  notes?: string;
  status?: string;
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

// ─── Paginated Responses ────────────────────────────────────────────────────

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ─── Filter Types ───────────────────────────────────────────────────────────

export interface ContractFilters {
  page?: number;
  pageSize?: number;
  patientId?: string;
  status?: string;
  specialty?: string;
  search?: string;
}

export interface PaymentFilters {
  page?: number;
  pageSize?: number;
  patientId?: string;
  contractId?: string;
  paymentMethod?: string;
  specialty?: string;
  from?: string;
  to?: string;
}

// ─── Overdue ────────────────────────────────────────────────────────────────

export interface OverdueContract {
  contractId: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  phone?: string;
  specialty?: string;
  totalAmount: number;
  paidAmount: number;
  overdueAmount: number;
  remainingAmount: number;
  monthsElapsed: number;
  startDate?: string;
}

// ─── Receipt ────────────────────────────────────────────────────────────────

export interface ReceiptData {
  receiptNumber: string;
  date: string;
  patientName: string;
  patientNumber: string;
  serviceDescription: string;
  amount: number;
  amountInWords: string;
  paymentMethod: string;
  receivedBy: string;
  centerName: string;
  centerAddress: string;
  centerPhones: string;
}

// ─── Finance Analytics ──────────────────────────────────────────────────────

export interface RevenueBySpecialty {
  specialty: string;
  total: number;
  count: number;
}

export interface RevenueByDoctor {
  doctorId: string;
  doctorName: string;
  total: number;
  count: number;
}

export interface DailyReport {
  date: string;
  totalCollected: number;
  paymentCount: number;
  byMethod: { method: string; total: number }[];
  bySpecialty: { specialty: string; total: number }[];
}

export interface MonthlyReport {
  year: number;
  month: number;
  totalCollected: number;
  paymentCount: number;
  newContracts: number;
  completedContracts: number;
  overdueContracts: number;
  dailyBreakdown: { date: string; total: number; count: number }[];
  bySpecialty: { specialty: string; total: number; count: number }[];
  byMethod: { method: string; total: number }[];
}

// ─── Report Types ───────────────────────────────────────────────────────────

export interface CenterSummary {
  fromDate: string;
  toDate: string;
  totalPatients: number;
  newPatients: number;
  totalAppointments: number;
  completedAppointments: number;
  activeOrthoCases: number;
  totalRevenue: number;
}

export interface DoctorPerformance {
  doctorId: string;
  name: string;
  color?: string;
  specialty?: string;
  appointmentCount: number;
  completedCount: number;
  orthoCasesCount: number;
  treatmentsCount: number;
  revenue: number;
}

export interface FinancialReport {
  fromDate: string;
  toDate: string;
  totalCollected: number;
  daily: { date: string; total: number; count: number }[];
  bySpecialty: { specialty: string; total: number; count: number }[];
  byMethod: { method: string; total: number }[];
}

export interface OrthoReport {
  fromDate: string;
  toDate: string;
  activeCases: number;
  completedCases: number;
  newCases: number;
  averageDuration: number;
  byStage: { stage: string; count: number }[];
  byDoctor: { doctorId: string; doctorName: string; count: number }[];
  monthlyCases: { month: string; count: number }[];
}

export interface GeneralDentistryReport {
  fromDate: string;
  toDate: string;
  totalTreatments: number;
  byType: { type: string; count: number; revenue: number }[];
  byDoctor: { doctorId: string; doctorName: string; count: number; revenue: number }[];
  topProcedures: { procedure: string; count: number }[];
}

export interface SurgeryReport {
  fromDate: string;
  toDate: string;
  totalCases: number;
  byType: { type: string; count: number }[];
  byStatus: { status: string; count: number }[];
  byDoctor: { doctorId: string; doctorName: string; count: number }[];
}

export interface GrowthAnalysis {
  months: { month: string; newPatients: number; returningPatients: number; total: number }[];
  totalNew: number;
  totalReturning: number;
  growthRate: number;
}

export interface OverdueReport {
  totalOverdue: number;
  contractCount: number;
  contracts: OverdueContract[];
  bySpecialty: { specialty: string; count: number; total: number }[];
}

// ─── Expense Types ──────────────────────────────────────────────────────────

export interface Expense {
  id: string;
  description: string;
  amount: number;
  expenseDate: string;
  category?: string;
  categoryAr?: string;
  paymentMethod?: string;
  paymentMethodAr?: string;
  recipient?: string;
  notes?: string;
  recordedByName?: string;
  createdAt?: string;
}

export interface CreateExpenseRequest {
  description: string;
  amount: number;
  expenseDate?: string;
  category?: string;
  paymentMethod?: string;
  recipient?: string;
  notes?: string;
}

export interface UpdateExpenseRequest {
  description?: string;
  amount?: number;
  expenseDate?: string;
  category?: string;
  paymentMethod?: string;
  recipient?: string;
  notes?: string;
}

export interface ExpenseSummary {
  todayExpenses: number;
  monthExpenses: number;
  totalExpenses: number;
  expenseCount: number;
  byCategory: {
    category: string;
    categoryAr: string;
    total: number;
    count: number;
  }[];
}

export interface ProfitLoss {
  fromDate: string;
  toDate: string;
  totalRevenue: number;
  totalExpenses: number;
  netProfit: number;
  profitMargin: number;
  revenue: { specialty: string; specialtyAr: string; totalRevenue: number }[];
  expenses: { category: string; categoryAr: string; total: number; count: number }[];
}
