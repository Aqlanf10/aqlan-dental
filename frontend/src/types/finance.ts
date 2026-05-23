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

/** Lightweight contract type used in list views — excludes detail-only fields */
export type ContractListItem = Omit<Contract, "discountAmount" | "discountReason" | "notes" | "payments">;

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
  patientId: string;
  patientName?: string;
  visitId?: string;
  appointmentId?: string;
  status: "Draft" | "Issued" | "Cancelled" | "Paid";
  statusArabic?: string;
  subtotal: number;
  discountAmount?: number;
  taxAmount?: number;
  totalAmount: number;
  lineItemCount?: number;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

export interface InvoiceDetail extends Invoice {
  createdBy?: string;
  updatedBy?: string;
  paidAmount?: number;
  remainingAmount?: number;
  lineItems: InvoiceLineItem[];
  payments?: InvoicePayment[];
}

export interface InvoicePayment {
  id: string;
  amount: number;
  paymentDate: string;
  paymentMethod?: string;
  receiptNumber?: string;
  notes?: string;
}

export interface InvoiceLineItem {
  id: string;
  invoiceId: string;
  serviceId?: string;
  serviceName?: string;
  serviceNameSnapshot: string;
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
  doctorId?: string;
  doctorName?: string;
  lineDiscountAmount?: number;
  materialCost?: number;
  labCost?: number;
  otherDirectCost?: number;
  commissionStatus?: string;
  doctorCommissionPercentage?: number;
  netCommissionableAmount?: number;
  doctorCommissionAmount?: number;
  centerShareAmount?: number;
  relatedTreatmentPlanStepId?: string;
  relatedVisitId?: string;
  sortOrder: number;
}

export interface UpdateInvoiceRequest {
  lineItems?: UpdateInvoiceLineItemRequest[];
  discountAmount?: number;
  taxAmount?: number;
  notes?: string;
}

export interface UpdateInvoiceLineItemRequest {
  serviceId?: string;
  serviceNameSnapshot?: string;
  description?: string;
  quantity: number;
  unitPrice: number;
  doctorId?: string;
  relatedTreatmentPlanStepId?: string;
  relatedVisitId?: string;
}
