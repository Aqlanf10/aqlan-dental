export type FinancePayment = {
  id: string;
  patientId: string;
  patientName?: string | null;
  contractId?: string | null;
  invoiceId?: string | null;
  invoiceNumber?: string | null;
  amount: number;
  currency?: string | null;
  accountCurrency?: string | null;
  exchangeRateToAccountCurrency?: number | null;
  exchangeRateToYer?: number | null;
  appliedAmount?: number | null;
  exchangeRateSource?: string | null;
  paymentDate: string;
  paymentMethod?: string | null;
  serviceDescription?: string | null;
  specialty?: string | null;
  doctorId?: string | null;
  doctorName?: string | null;
  receiptNumber?: string | null;
  notes?: string | null;
  isActive?: boolean;
  createdAt?: string | null;
  updatedAt?: string | null;
};

export type ContractStatement = {
  id: string;
  specialty?: string | null;
  totalAmount: number;
  discountAmount: number;
  paidAmount: number;
  remainingAmount: number;
  startDate?: string | null;
  status: string;
  installmentsCount: number;
  installmentAmount?: number | null;
};

export type AccountStatement = {
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
  totalPaymentsCount?: number | null;
  payments?: FinancePayment[] | null;
  recentPayments: FinancePayment[];
};

export type FinanceInvoice = {
  id: string;
  invoiceNumber: string;
  patientId?: string | null;
  patientName?: string | null;
  visitId?: string | null;
  appointmentId?: string | null;
  status: string;
  statusArabic?: string | null;
  subtotal?: number | null;
  discountAmount?: number | null;
  taxAmount?: number | null;
  totalAmount: number;
  paidAmount?: number | null;
  balance?: number | null;
  lineItemCount?: number | null;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
};

export type FinanceContract = {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  specialty?: string | null;
  totalAmount: number;
  downPayment: number;
  paidAmount: number;
  remainingAmount: number;
  installmentsCount: number;
  installmentAmount?: number | null;
  startDate?: string | null;
  status: string;
};

export type ActiveCashierSession = {
  id: string;
  sessionNumber: string;
  openedAt: string;
  cashierName: string;
  expectedClosingCash: number;
  expectedClosingCard: number;
  expectedClosingBank: number;
  hasActiveSession?: boolean;
};

export type CreatePaymentInput = {
  patientId: string;
  contractId?: string | null;
  invoiceId?: string | null;
  amount: number;
  currency?: string;
  accountCurrency?: string;
  exchangeRateToAccountCurrency?: number;
  exchangeRateToYer?: number;
  exchangeRateSource?: string;
  paymentMethod?: string;
  serviceDescription?: string | null;
  specialty?: string | null;
  doctorId?: string | null;
  notes?: string | null;
};

export const PAYMENT_METHOD_OPTIONS = [
  { value: "cash", label: "نقداً" },
  { value: "bank_transfer", label: "تحويل بنكي" },
  { value: "card", label: "بطاقة" },
  { value: "other", label: "أخرى" }
] as const;

export const PAYMENT_METHOD_LABELS: Record<string, string> = {
  cash: "نقداً",
  Cash: "نقداً",
  bank_transfer: "تحويل بنكي",
  BankTransfer: "تحويل بنكي",
  transfer: "تحويل",
  card: "بطاقة",
  Card: "بطاقة",
  check: "شيك",
  other: "أخرى"
};

export const INVOICE_STATUS_LABELS: Record<string, string> = {
  Draft: "مسودة",
  Issued: "مصدرة",
  Paid: "مدفوعة",
  Cancelled: "ملغاة"
};
