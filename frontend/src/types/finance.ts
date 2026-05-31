// Finance types — shared across patient module (FinanceTab, PaymentsTab, ContractsTab, receipts, print)
// Dead V1/V2 types removed: OverdueContract, ContractListItem, FinanceSummary,
// CreateContractRequest, InvoiceDetail, InvoicePayment, InvoiceLineItem,
// UpdateInvoiceRequest, UpdateInvoiceLineItemRequest, ContractStatement

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
  // Detail-only fields — only present in GET /api/contracts/{id}
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
  invoiceId?: string;
  invoiceNumber?: string;
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

export interface CreatePaymentRequest {
  patientId: string;
  contractId?: string;
  invoiceId?: string;
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

export interface Invoice {
  id: string;
  invoiceNumber: string;
  patientId?: string;
  patientName?: string;
  visitId?: string;
  appointmentId?: string;
  status: string;
  statusArabic?: string;
  subtotal?: number;
  discountAmount?: number;
  taxAmount?: number;
  totalAmount: number;
  paidAmount?: number;
  balance?: number;
  lineItemCount?: number;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

/** Account statement as returned by GET /api/patients/{id}/account-statement */
export interface AccountStatement {
  patientId: string;
  patientName: string;
  patientNumber: string;
  totalContracted: number;
  totalDiscounts: number;
  totalPaid: number;
  totalRemaining: number;
  activeContracts: number;
  completedContracts: number;
  contracts: ContractStatement[];
  recentPayments: Payment[];
}

/** Single contract within an account statement */
export interface ContractStatement {
  id: string;
  specialty?: string;
  totalAmount: number;
  discountAmount: number;
  paidAmount: number;
  remainingAmount: number;
  startDate?: string;
  status: string;
  installmentsCount: number;
  installmentAmount?: number;
}

// ─── Centralized Finance Constants ──────────────────────────────────────

export const PAYMENT_METHODS = {
  cash: "نقداً",
  bank_transfer: "تحويل بنكي",
  transfer: "تحويل",
  card: "بطاقة",
  check: "شيك",
  other: "أخرى",
} as const;

export const PAYMENT_METHOD_OPTIONS = [
  { value: "cash", label: "نقداً" },
  { value: "bank_transfer", label: "تحويل بنكي" },
  { value: "card", label: "بطاقة" },
  { value: "other", label: "أخرى" },
] as const;

export const SPECIALTY_OPTIONS = {
  orthodontics: "تقويم أسنان",
  general: "طب أسنان عام",
  surgery: "جراحة فم",
  oral_surgery: "جراحة فم",
  periodontics: "لثة",
  endodontics: "علاج عصب",
  prosthodontics: "تركيبات",
  other: "أخرى",
  // PascalCase aliases (backend may return either)
  Orthodontics: "تقويم أسنان",
  General: "طب أسنان عام",
  OralSurgery: "جراحة فم",
  Periodontics: "لثة",
  Endodontics: "علاج عصب",
  Prosthodontics: "تركيبات",
} as const;

export const CONTRACT_STATUS = {
  active: { label: "نشط", color: "bg-green-50 text-green-700" },
  completed: { label: "مكتمل", color: "bg-blue-50 text-blue-700" },
  cancelled: { label: "ملغى", color: "bg-red-50 text-red-600" },
} as const;

export const INVOICE_STATUS = {
  Draft: { label: "مسودة", color: "bg-blue-50 text-blue-700" },
  Issued: { label: "مصدرة", color: "bg-green-50 text-green-700" },
  Cancelled: { label: "ملغاة", color: "bg-gray-100 text-gray-500" },
  Paid: { label: "مدفوعة", color: "bg-emerald-50 text-emerald-700" },
} as const;
