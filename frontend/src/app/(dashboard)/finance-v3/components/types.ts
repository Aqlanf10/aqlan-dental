/* ═══════════════════════════════════════════════════════════════════════════════
   Finance V3 — Type Definitions
   ═══════════════════════════════════════════════════════════════════════════════ */

/* ── Dashboard Overview ─────────────────────────────────────────────────────────── */
export interface DashboardData {
  TodayInflow: number;
  TodayOutflow: number;
  TodayNet: number;
  MonthInflow: number;
  MonthOutflow: number;
  MonthNet: number;
  TotalOutstanding: number;
  ContractOutstanding: number;
  InvoiceOutstanding: number;
  TotalTreasuryBalance: number;
  TodayAccruedRevenue: number;
  MonthAccruedRevenue: number;
  JournalEntryCount: number;
  PostedEntryCount: number;
  ReversalEntryCount: number;
  DualWriteCoverage: string;
  PendingExpenses: number;
  PendingTransfers: number;
  Date: string;
}

/* ── Patient Accounts ───────────────────────────────────────────────────────────── */
export interface PatientBalance {
  PatientId: string;
  PatientName: string;
  PatientNumber: string;
  TotalInvoiced: number;
  TotalPaid: number;
  TotalRefunds: number;
  Balance: number;
  OutstandingInvoices: number;
  ActiveContracts: number;
  HasOutstanding: boolean;
}

export interface PatientBalanceDetail {
  PatientId: string;
  PatientName: string;
  PatientNumber: string;
  TotalInvoiced: number;
  TotalPaid: number;
  TotalRefunds: number;
  NetPaid: number;
  TotalDiscounts: number;
  Balance: number;
  EntityBalance?: number;
  ContractOutstanding: number;
  HasOutstanding: boolean;
  JournalReceivable?: number;
  JournalAdvance?: number;
}

/* ── Invoices ───────────────────────────────────────────────────────────────────── */
export interface InvoiceListItem {
  Id: string;
  InvoiceNumber: string;
  PatientId: string;
  PatientName: string;
  PatientNumber: string;
  TotalAmount: number;
  PaidAmount: number;
  Balance: number;
  Status: string;
  IssueDate: string;
  CreatedAt: string;
}

export interface InvoiceLineItem {
  Id: string;
  TreatmentId: string | null;
  TreatmentName: string;
  ToothNumber: string | null;
  Quantity: number;
  UnitPrice: number;
  DiscountAmount: number;
  TotalPrice: number;
}

export interface InvoiceDetail {
  Id: string;
  InvoiceNumber: string;
  PatientId: string;
  PatientName: string;
  PatientNumber: string;
  LineItems: InvoiceLineItem[];
  Subtotal: number;
  TotalDiscount: number;
  TotalAmount: number;
  PaidAmount: number;
  Balance: number;
  Status: string;
  IssueDate: string;
  DueDate: string | null;
  ContractId: string | null;
  Notes: string | null;
}

/* ── Collections / Payments ─────────────────────────────────────────────────────── */
export interface PaymentListItem {
  Id: string;
  PaymentNumber: string;
  PatientId: string;
  PatientName: string;
  PatientNumber: string;
  Amount: number;
  PaymentMethod: string;
  PaymentDate: string;
  InvoiceId: string | null;
  ContractId: string | null;
  CashierSessionId: string | null;
  IsReversal: boolean;
  ReversedById: string | null;
  Status: string;
}

export interface RegisterPaymentRequest {
  PatientId: string;
  InvoiceId?: string;
  ContractId?: string;
  Amount: number;
  PaymentMethod: string;
  Notes?: string;
}

/* ── Contracts ──────────────────────────────────────────────────────────────────── */
export interface ContractListItem {
  Id: string;
  ContractNumber: string;
  PatientId: string;
  PatientName: string;
  PatientNumber: string;
  TotalAmount: number;
  PaidAmount: number;
  OutstandingAmount: number;
  Status: string;
  StartDate: string;
  EndDate: string | null;
  IsOverdue: boolean;
}

/* ── Cashier Sessions ───────────────────────────────────────────────────────────── */
export interface CashierSession {
  Id: string;
  CashierUserId?: string;
  CashierId?: string;
  CashierName: string;
  BranchId: string;
  OpenedAt: string;
  ClosingTime: string | null;
  OpeningBalance: number;
  ExpectedClosingCash: number;
  ExpectedClosingCard: number;
  ExpectedClosingBank: number;
  ActualClosingCash: number | null;
  ActualClosingCard: number | null;
  ActualClosingBank: number | null;
  ShortageOrSurplus: number | null;
  Status: string;
  Notes: string | null;
  TreasuryId: string | null;
}

export interface CloseSessionRequest {
  ActualClosingCash: number;
  ActualClosingCard: number;
  ActualClosingBank: number;
}

/* ── Treasuries ─────────────────────────────────────────────────────────────────── */
export interface Treasury {
  Id: string;
  Name: string;
  Type: string;
  TypeArabic?: string;
  Balance: number;
  BranchId: string;
}

export interface CreateTreasuryRequest {
  Name: string;
  Type: string;
  OpeningBalance: number;
  BranchId?: string;
}

export interface VaultTransfer {
  Id: string;
  SourceTreasuryId: string;
  SourceTreasuryName: string;
  DestinationTreasuryId: string;
  DestinationTreasuryName: string;
  Amount: number;
  DepositSource: string | null;
  Status: string;
  RequestedBy: string;
  RequestedAt: string;
  ApprovedBy: string | null;
  ApprovedAt: string | null;
  RejectedBy: string | null;
  RejectedAt: string | null;
  RejectionReason: string | null;
}

export interface CreateTransferRequest {
  SourceTreasuryId: string;
  DestinationTreasuryId: string;
  Amount: number;
  DepositSource?: string;
  Notes?: string;
}

/* ── Expenses ───────────────────────────────────────────────────────────────────── */
export interface ExpenseListItem {
  Id: string;
  Title: string;
  Category: string;
  Amount: number;
  PaymentMethod: string;
  ExpenseDate: string;
  Status: string;
  RequestedBy: string;
  ApprovedBy: string | null;
  ApprovedAt: string | null;
  RejectedBy: string | null;
  RejectedAt: string | null;
  RejectionReason: string | null;
  IsReversal: boolean;
  TreasuryId: string | null;
  TreasuryName: string | null;
}

export interface CreateExpenseRequest {
  Title: string;
  Category: string;
  Amount: number;
  PaymentMethod: string;
  ExpenseDate: string;
  TreasuryId?: string;
  Notes?: string;
}

/* ── Suppliers ──────────────────────────────────────────────────────────────────── */
export interface SupplierListItem {
  Id: string;
  Name: string;
  ContactPerson: string | null;
  Phone: string | null;
  TotalBilled: number;
  TotalPaid: number;
  Balance: number;
}

export interface SupplierBill {
  Id: string;
  SupplierId: string;
  SupplierName: string;
  Description: string;
  TotalAmount: number;
  PaidAmount: number;
  Balance: number;
  DueDate: string;
  Status: string;
  CreatedAt: string;
}

export interface CreateSupplierBillRequest {
  SupplierId: string;
  Description: string;
  TotalAmount: number;
  DueDate: string;
  Notes?: string;
}

export interface PaySupplierBillRequest {
  Amount: number;
  PaymentMethod: string;
  TreasuryId?: string;
  Notes?: string;
}

/* ── Profit & Loss ──────────────────────────────────────────────────────────────── */
export interface ProfitLossData {
  Period: { From: string; To: string };
  AccruedRevenue: number;
  AccruedExpenses: number;
  AccruedNetProfit: number;
  CashCollections: number;
  CashRefunds: number;
  PatientPaymentReversals?: number;
  NetCashCollections: number;
  OperatingExpenses: number;
  SalaryPayments: number;
  DoctorCommissions: number;
  SupplierPayments: number;
  TotalCosts: number;
  CashNetProfit: number;
  ProfitMargin: number;
  ReversalCoverage: Record<string, string>;
  RevenueTransactionCount?: number;
  ExpenseTransactionCount?: number;
}

/* ── Daily Cash Summary ─────────────────────────────────────────────────────────── */
export interface DailyCashCategory {
  Type: string;
  Category: string;
  IsReversal: boolean;
  Count: number;
  Total: number;
}

export interface DailyCashSummary {
  Date: string;
  TotalInflow: number;
  TotalOutflow: number;
  NetCash: number;
  ByCategory: DailyCashCategory[];
  ByPaymentMethod: { PaymentMethod: string; Count: number; Total: number }[];
  TransactionCount: number;
  ReversalCount: number;
  JournalEntryCount: number;
}

/* ── Account Balances ───────────────────────────────────────────────────────────── */
export interface AccountBalance {
  AccountType: string;
  TotalDebit: number;
  TotalCredit: number;
  NetBalance: number;
  EntryCount: number;
}

export interface AccountBalancesData {
  AccountBalances: AccountBalance[];
  Treasuries: { Id: string; Name: string; Type: string; Balance: number; BranchId: string }[];
  TotalAssets: number;
  TotalRevenue: number;
  TotalExpenses: number;
  TotalReceivables: number;
  TotalPayables: number;
}

/* ── Expense categories ─────────────────────────────────────────────────────────── */
export const EXPENSE_CATEGORIES = [
  { value: "Rent", label: "إيجار" },
  { value: "Utilities", label: "مرافق" },
  { value: "Supplies", label: "مستلزمات" },
  { value: "Maintenance", label: "صيانة" },
  { value: "Marketing", label: "تسويق" },
  { value: "Transportation", label: "نقل" },
  { value: "Insurance", label: "تأمين" },
  { value: "Food", label: "ضيافة" },
  { value: "ProfessionalServices", label: "خدمات مهنية" },
  { value: "Other", label: "أخرى" },
] as const;

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
