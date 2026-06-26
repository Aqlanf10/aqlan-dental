/* ═══════════════════════════════════════════════════════════════════════════════
   Finance V3 — Type Definitions
   ═══════════════════════════════════════════════════════════════════════════════

   IMPORTANT: Property names use camelCase to match the JSON output from
   ASP.NET Core's default JsonSerializerDefaults.Web naming policy.
   The FinanceV3Controller returns anonymous objects with PascalCase C# names,
   but ASP.NET Core's CamelCaseNamingPolicy converts them to camelCase in JSON.
   
   All component code accesses these properties with camelCase (e.g., `r.totalAmount`),
   matching the actual API response format.
   ═══════════════════════════════════════════════════════════════════════════════ */

/* ── Dashboard Overview ─────────────────────────────────────────────────────────── */
export interface DashboardData {
  todayInflow: number;
  todayOutflow: number;
  todayNet: number;
  monthInflow: number;
  monthOutflow: number;
  monthNet: number;
  totalOutstanding: number;
  contractOutstanding: number;
  invoiceOutstanding: number;
  totalTreasuryBalance: number;
  todayAccruedRevenue: number;
  monthAccruedRevenue: number;
  journalEntryCount: number;
  postedEntryCount: number;
  reversalEntryCount: number;
  dualWriteCoverage: string;
  pendingExpenses: number;
  pendingTransfers: number;
  date: string;
  isConsolidated?: boolean;
  activeContracts?: number;
  unpaidInvoicesCount?: number;
  draftInvoicesCount?: number;
  overdueAmount?: number;
  pendingCommissionsAmount?: number;
}

/* ── Patient Accounts ───────────────────────────────────────────────────────────── */
export interface PatientBalance {
  patientId: string;
  patientName: string;
  patientNumber: string;
  totalInvoiced: number;
  totalPaid: number;
  totalRefunds: number;
  balance: number;
  outstandingInvoices: number;
  activeContracts: number;
  hasOutstanding: boolean;
}

export interface PatientBalanceDetail {
  patientId: string;
  patientName: string;
  patientNumber: string;
  totalInvoiced: number;
  totalPaid: number;
  totalRefunds: number;
  netPaid: number;
  totalDiscounts: number;
  balance: number;
  entityBalance?: number;
  contractOutstanding: number;
  hasOutstanding: boolean;
  journalReceivable?: number;
  journalAdvance?: number;
}

/* ── Invoices ───────────────────────────────────────────────────────────────────── */
export interface InvoiceListItem {
  id: string;
  invoiceNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  totalAmount: number;
  paidAmount: number;
  balance: number;
  status: string;
  issueDate: string;
  createdAt: string;
}

export interface InvoiceLineItem {
  id: string;
  treatmentId: string | null;
  treatmentName: string;
  toothNumber: string | null;
  quantity: number;
  unitPrice: number;
  discountAmount: number;
  totalPrice: number;
}

export interface InvoiceDetail {
  id: string;
  invoiceNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  lineItems: InvoiceLineItem[];
  subtotal: number;
  totalDiscount: number;
  totalAmount: number;
  paidAmount: number;
  balance: number;
  status: string;
  issueDate: string;
  dueDate: string | null;
  contractId: string | null;
  notes: string | null;
}

/* ── Collections / Payments ─────────────────────────────────────────────────────── */
export interface PaymentListItem {
  id: string;
  paymentNumber: string;
  /** receiptNumber from backend — same value as paymentNumber (alias). Prefer this for display. */
  receiptNumber?: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  amount: number;
  currency?: string | null;
  accountCurrency?: string | null;
  exchangeRateToAccountCurrency?: number;
  appliedAmount?: number;
  exchangeRateSource?: string | null;
  paymentMethod: string;
  paymentDate: string;
  invoiceId: string | null;
  contractId: string | null;
  isReversal: boolean;
  reversedById: string | null;
  status: string;
  /** Whether this payment has a corresponding JournalEntry (reconciliation indicator) */
  hasJournalEntry?: boolean;
}

export interface RegisterPaymentRequest {
  patientId: string;
  invoiceId?: string;
  contractId?: string;
  amount: number;
  paymentMethod: string;
  currency?: string | null;
  accountCurrency?: string | null;
  exchangeRateToAccountCurrency?: number | null;
  exchangeRateSource?: string | null;
  notes?: string;
}

/* ── Contracts ──────────────────────────────────────────────────────────────────── */
export interface ContractListItem {
  id: string;
  contractNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  /** Specialty field from Contract entity (used as display label when contractNumber is generated) */
  specialty?: string;
  totalAmount: number;
  paidAmount: number;
  outstandingAmount: number;
  status: string;
  startDate: string;
  isOverdue: boolean;
  /** YOLO-S2: optional TreatmentPackage link (display-only). */
  packageId?: string | null;
  packageName?: string | null;
  packageColor?: string | null;
}

/* ── Cashier Sessions ───────────────────────────────────────────────────────────── */
export interface CashierSession {
  id: string;
  cashierId?: string;
  cashierName: string;
  branchId: string;
  openedAt: string;
  closingTime: string | null;
  openingBalance: number;
  expectedClosingCash: number;
  expectedClosingCard: number;
  expectedClosingBank: number;
  actualClosingCash: number | null;
  actualClosingCard: number | null;
  actualClosingBank: number | null;
  shortageOrSurplus: number | null;
  status: string;
  notes: string | null;
  treasuryId: string | null;
}

export interface CloseSessionRequest {
  actualClosingCash: number;
  actualClosingCard: number;
  actualClosingBank: number;
  notes?: string;
}

/* ── Treasuries ─────────────────────────────────────────────────────────────────── */
export interface Treasury {
  id: string;
  name: string;
  type: string;
  currency: "YER" | "SAR" | "USD";
  balance: number;
  branchId: string;
}

export interface CreateTreasuryRequest {
  name: string;
  type: string;
  currency?: "YER" | "SAR" | "USD";
  openingBalance: number;
  branchId?: string;
}

export interface VaultTransfer {
  id: string;
  sourceTreasuryId: string;
  sourceTreasuryName: string;
  destinationTreasuryId: string;
  destinationTreasuryName: string;
  amount: number;
  depositSource: string | null;
  status: string;
  performedBy: string;
  transferDate: string;
  approvedBy: string | null;
  approvalDate: string | null;
  rejectedBy: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
}

export interface CreateTransferRequest {
  sourceTreasuryId: string;
  destinationTreasuryId: string;
  amount: number;
  depositSource?: string;
  notes?: string;
}

/* ── Expenses ───────────────────────────────────────────────────────────────────── */
export interface ExpenseListItem {
  id: string;
  title: string;
  category: string;
  amount: number;
  paymentMethod: string;
  expenseDate: string;
  status: string;
  requestedBy: string;
  approvedBy: string | null;
  approvedAt: string | null;
  rejectedBy: string | null;
  rejectedAt: string | null;
  rejectionReason: string | null;
  isReversal: boolean;
  treasuryId: string | null;
  treasuryName: string | null;
}

export interface CreateExpenseRequest {
  title: string;
  category: string;
  amount: number;
  paymentMethod: string;
  expenseDate: string;
  treasuryId?: string;
  notes?: string;
}

/* ── Suppliers ──────────────────────────────────────────────────────────────────── */
export type SupplierType = 'DentalLab' | 'MedicalVendor' | 'GeneralService';
export type BillStatus = 'Unpaid' | 'PartiallyPaid' | 'FullyPaid' | 'Cancelled';

export interface SupplierListItem {
  id: string;
  name: string;
  contactPerson: string | null;
  phone: string | null;
  totalBilled: number;
  totalPaid: number;
  balance: number;
}

/** Matches FinanceV3SuppliersController GET /api/finance-v3/suppliers response */
export interface SupplierDto {
  id: string;
  name: string;
  type: number;  // Backend serializes SupplierType enum as integer (0=DentalLab, 1=MedicalVendor, 2=GeneralService)
  typeLabel?: string;  // Arabic label if provided
  contactPerson: string | null;
  phone: string | null;
  totalBilled: number;
  totalPaid: number;
  balance: number;
  isActive: boolean;
}

/** Matches FinanceV3SuppliersController GET /{id}/bills response items */
export interface SupplierBillDto {
  id: string;
  billNumber: string;
  description: string;
  totalAmount: number;
  paidAmount: number;
  remainingAmount: number;
  status: BillStatus;
  billDate: string;
  dueDate: string | null;
}

/** Supplier account statement from GET /{id}/bills */
export interface SupplierStatement {
  supplierId: string;
  supplierName: string;
  balance: number;
  bills: SupplierBillDto[];
}

export interface SupplierBill {
  id: string;
  supplierId: string;
  supplierName: string;
  description: string;
  totalAmount: number;
  paidAmount: number;
  balance: number;
  dueDate: string;
  status: string;
  createdAt: string;
}

export interface CreateSupplierBillRequest {
  supplierId: string;
  description: string;
  totalAmount: number;
  dueDate: string;
  notes?: string;
}

/** Matches CreateFinanceV3SupplierRequest DTO */
export interface CreateSupplierRequest {
  name: string;
  contactPerson?: string;
  phone?: string;
  type?: SupplierType;
}

export interface PaySupplierBillRequest {
  amount: number;
  paymentMethod: string;
  treasuryId?: string;
  referenceNumber?: string;
  notes?: string;
}

/** Supplier type labels for Arabic UI */
export const SUPPLIER_TYPE_LABELS: Record<SupplierType, string> = {
  DentalLab: 'معمل تركيبات',
  MedicalVendor: 'مورد طبي',
  GeneralService: 'خدمات عامة',
} as const;

export const SUPPLIER_TYPE_OPTIONS = [
  { value: 'DentalLab', label: 'معمل تركيبات' },
  { value: 'MedicalVendor', label: 'مورد طبي' },
  { value: 'GeneralService', label: 'خدمات عامة' },
] as const;

export const SUPPLIER_TYPE_MAP: Record<number, SupplierType> = {
  0: 'DentalLab',
  1: 'MedicalVendor',
  2: 'GeneralService',
};

export function getSupplierTypeLabel(typeNum: number): string {
  const key = SUPPLIER_TYPE_MAP[typeNum] ?? 'MedicalVendor';
  return SUPPLIER_TYPE_LABELS[key];
}

/* ── Profit & Loss ──────────────────────────────────────────────────────────────── */
export interface ProfitLossData {
  period: { from: string; to: string };
  accruedRevenue: number;
  accruedExpenses: number;
  accruedNetProfit: number;
  cashCollections: number;
  cashRefunds: number;
  patientPaymentReversals?: number;
  netCashCollections: number;
  operatingExpenses: number;
  salaryPayments: number;
  doctorCommissions: number;
  supplierPayments: number;
  totalCosts: number;
  cashNetProfit: number;
  profitMargin: number;
  reversalCoverage: Record<string, string>;
  revenueTransactionCount?: number;
  expenseTransactionCount?: number;
  isConsolidated?: boolean;
  netCashCollectionsFormula?: string;
}

/* ── Daily Cash Summary ─────────────────────────────────────────────────────────── */
export interface DailyCashCategory {
  type: string;
  category: string;
  isReversal: boolean;
  count: number;
  total: number;
}

export interface DailyCashSummary {
  date: string;
  totalInflow: number;
  totalOutflow: number;
  netCash: number;
  byCategory: DailyCashCategory[];
  byPaymentMethod: { paymentMethod: string; count: number; total: number }[];
  transactionCount: number;
  reversalCount: number;
  journalEntryCount: number;
  isConsolidated?: boolean;
}

/* ── Account Balances ───────────────────────────────────────────────────────────── */
export interface AccountBalance {
  accountType: string;
  totalDebit: number;
  totalCredit: number;
  netBalance: number;
  entryCount: number;
}

export interface AccountBalancesData {
  accountBalances: AccountBalance[];
  treasuries: { id: string; name: string; type: string; balance: number; branchId: string }[];
  totalAssets: number;
  totalRevenue: number;
  totalExpenses: number;
  totalReceivables: number;
  totalPayables: number;
  isConsolidated?: boolean;
}

/* ── Expense categories ─────────────────────────────────────────────────────────── */
export const EXPENSE_CATEGORIES = [
  { value: "Rent", label: "إيجار" },
  { value: "Utilities", label: "مرافق" },
  { value: "LabFees", label: "رسوم مختبر" },
  { value: "Marketing", label: "تسويق" },
  { value: "ClinicSupplies", label: "مستلزمات العيادة" },
  { value: "Maintenance", label: "صيانة" },
  { value: "Salaries", label: "رواتب" },
  { value: "Commissions", label: "عمولات" },
  { value: "Taxes", label: "ضرائب" },
  { value: "Miscellaneous", label: "أخرى" },
] as const;

/* ── Credit Notes & Refunds ─────────────────────────────────────────────────────── */
export type CreditNoteStatus = 'Draft' | 'Approved' | 'Refunded';

export interface CreditNoteDto {
  id: string;
  invoiceId: string;
  patientId: string;
  amount: number;
  reason: string;
  status: CreditNoteStatus;
  createdAt: string;
  refundPaymentId?: string;
}

export interface CreateCreditNoteRequest {
  invoiceId: string;
  amount: number;
  reason: string;
}

export interface ProcessRefundRequest {
  treasuryId: string;
  notes: string;
  paymentMethod?: string;  // defaults to "cash" on backend
}

/* ── Deposit sources for external transfers ─────────────────────────────────────── */
export const DEPOSIT_SOURCES = [
  { value: "OwnerCapital", label: "رأس مال المالك" },
  { value: "OpeningBalance", label: "رصيد افتتاحي" },
  { value: "OtherReceivable", label: "ذمم مدينة أخرى" },
  { value: "AuthorizedRevenueDocument", label: "مستند إيراد معتمد" },
] as const;

/* ── Payment methods ────────────────────────────────────────────────────────────── */
export const PAYMENT_METHODS = [
  { value: "cash", label: "نقدي" },
  { value: "card", label: "بطاقة" },
  { value: "bank_transfer", label: "تحويل بنكي" },
] as const;

/* ── Treasury types ─────────────────────────────────────────────────────────────── */
export const TREASURY_TYPES = [
  { value: "Vault", label: "خزنة نقدية" },
  { value: "Bank", label: "حساب بنكي" },
] as const;
