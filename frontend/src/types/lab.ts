export interface LabOrder {
  id: string;
  orderNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  orthoCaseNumber?: string;
  applianceType: string;
  labName?: string;
  labEntityName?: string;
  labId?: string;
  sentDate?: string;
  expectedDate?: string;
  receivedDate?: string;
  deliveredDate?: string;
  status: LabOrderStatus;
  priority: LabOrderPriority;
  instructions?: string;
  cost?: number;
  totalCost?: number;
  currency?: "YER" | "SAR" | "USD";
  exchangeRateToYer?: number;
  doctorName?: string;
  shade?: string;
  restorationType?: string;
  visitId?: string;
  cancellationReason?: string;
  createdAt: string;
  // Lab Sprint 4 — Remake/Return fields
  remakeReason?: string;
  returnReason?: string;
  remakeCost?: number;
  isFreeRemake?: boolean;
  originalOrderId?: string;
  remakeCount?: number;
  // Lab Sprint 3 — order items
  items?: LabOrderItemResponse[];
}

export type LabOrderStatus = "draft" | "sent" | "manufacturing" | "tryIn" | "ready" | "received" | "delivered" | "returned" | "remake" | "cancelled";
export type LabOrderPriority = "urgent" | "normal" | "low";

export interface LabOrderItemResponse {
  id: string;
  workTypeId: string;
  workTypeName?: string;
  toothNumber?: string;
  arch?: string;
  shade?: string;
  restorationType?: string;
  unitsCount: number;
  unitPrice?: number;
  totalPrice?: number;
  instructions?: string;
  sortOrder: number;
}

export interface CreateLabOrderItemDto {
  workTypeId: string;
  toothNumber?: string;
  arch?: string;
  shade?: string;
  restorationType?: string;
  unitsCount?: number;
  unitPrice?: number;
  totalPrice?: number;
  instructions?: string;
  sortOrder?: number;
}

export interface CreateLabOrderRequest {
  patientId: string;
  orthoCaseId?: string;
  applianceType: string;
  labName?: string;
  labId?: string;
  sentDate?: string;
  expectedDate?: string;
  priority?: LabOrderPriority;
  instructions?: string;
  cost?: number;
  currency?: "YER" | "SAR" | "USD";
  exchangeRateToYer?: number;
  doctorId?: string;
  shade?: string;
  restorationType?: string;
  visitId?: string;
  // Lab Sprint 3 — professional order items
  items?: CreateLabOrderItemDto[];
}

export interface UpdateLabOrderStatusRequest {
  status: LabOrderStatus;
  receivedDate?: string;
}

// Lab Sprint 2 — Lab management
export interface Lab {
  id: string;
  name: string;
  phone?: string;
  whatsApp?: string;
  address?: string;
  contactPerson?: string;
  email?: string;
  notes?: string;
  branchId?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface LabWorkType {
  id: string;
  name: string;
  nameAr?: string;
  category?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

// Lab Sprint 3 — Pricing
export interface LabWorkPrice {
  id: string;
  labId: string;
  labName?: string;
  workTypeId: string;
  workTypeName?: string;
  workTypeNameAr?: string;
  unitPrice: number;
  urgentSurcharge?: number;
  urgentSurchargeType?: string;
  estimatedDays?: number;
  notes?: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateLabWorkPriceRequest {
  labId: string;
  workTypeId: string;
  unitPrice: number;
  urgentSurcharge?: number;
  urgentSurchargeType?: string;
  estimatedDays?: number;
  notes?: string;
}

export interface UpdateLabWorkPriceRequest {
  unitPrice: number;
  urgentSurcharge?: number;
  urgentSurchargeType?: string;
  estimatedDays?: number;
  notes?: string;
}

// Lab Sprint 4 — Status History
export interface LabOrderStatusHistoryEntry {
  id: string;
  fromStatus: string;
  toStatus: string;
  changedByName?: string;
  reason?: string;
  createdAt: string;
}

// Lab Sprint 4 — Attachments
export interface LabOrderAttachment {
  id: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  category: string;
  labOrderItemId?: string;
  storagePath: string;
  uploadedByName?: string;
  createdAt: string;
}

export interface ReturnLabOrderRequest {
  reason: string;
}

export interface RemakeLabOrderRequest {
  reason: string;
  isFreeRemake?: boolean;
  remakeCost?: number;
}

// Lab Sprint 5 — Payables
export interface LabPayable {
  id: string;
  labId?: string;
  labOrderId: string;
  labName?: string;
  orderNumber?: string;
  patientName?: string;
  amount: number;
  paidAmount: number;
  balance: number;
  status: "pending" | "partial" | "paid";
  currency: "YER" | "SAR" | "USD";
  exchangeRateToYer: number;
  supplierBillId?: string;
  dueDate?: string;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

export interface RecordLabPaymentRequest {
  amount: number;
  paymentMethod?: string;
  treasuryId?: string;
  exchangeRateToYer?: number;
  referenceNumber?: string;
  notes?: string;
}

// Lab Sprint 6 — Reports & Analytics
/**
 * CORE-LAB-010: lab money is never one number. A lab order carries its own currency, so every
 * total in the lab reports is a list of per-currency amounts. Currencies with nothing in them
 * are omitted, so a single-currency clinic still sees exactly one entry.
 */
export interface LabCurrencyAmount {
  currency: string;
  amount: number;
}

export interface LabCostReportItem {
  labId?: string;
  labName: string;
  totalOrders: number;
  /** CORE-LAB-011: committed orders only — drafts and cancellations are not a cost. */
  totalCostByCurrency: LabCurrencyAmount[];
  pendingOrders: number;
  returnedOrders: number;
  remakeOrders: number;
}

export interface LabDebtSummary {
  totalDebtByCurrency: LabCurrencyAmount[];
  totalPayables: number;
  pendingCount: number;
  partialCount: number;
}

export interface LabPerformanceItem {
  labId?: string;
  labName: string;
  totalOrders: number;
  deliveredOrders: number;
  remakeOrders: number;
  overdueOrders: number;
  cancelledOrders: number;
  totalCostByCurrency: LabCurrencyAmount[];
  avgExecutionDays: number;
  remakeRate: number;
  overdueRate: number;
  onTimeRate: number;
}

export interface LabPerformanceSummary {
  totalLabs: number;
  totalOrders: number;
  totalDelivered: number;
  totalOverdue: number;
  totalRemakes: number;
  totalCostByCurrency: LabCurrencyAmount[];
  overallAvgExecutionDays: number;
  overallRemakeRate: number;
  overallOverdueRate: number;
}

export interface LabDashboardKPIs {
  totalOrders: number;
  pendingOrders: number;
  readyOrders: number;
  receivedOrders: number;
  overdueOrders: number;
  /** Renamed from deliveredThisMonth: the window is the last 30 clinic days, not a calendar month. */
  deliveredLast30Days: number;
  returnedOrders: number;
  remakeOrders: number;
  totalLabCostsByCurrency: LabCurrencyAmount[];
  totalDebtByCurrency: LabCurrencyAmount[];
}

export interface StatusDistributionItem {
  status: string;
  count: number;
}

export interface TopLabItem {
  labId?: string;
  labName: string;
  orderCount: number;
  totalCostByCurrency: LabCurrencyAmount[];
}

export interface OverdueOrderItem {
  id: string;
  orderNumber: string;
  patientName: string;
  labName?: string;
  labEntityName?: string;
  applianceType: string;
  expectedDate?: string;
  daysOverdue: number;
  status: string;
  priority: string;
}

export interface MonthlyTrendItem {
  year: number;
  month: number;
  totalOrders: number;
  deliveredOrders: number;
  totalCostByCurrency: LabCurrencyAmount[];
}

/**
 * CORE-LAB-016: the clinic's running account with one lab, per currency.
 *
 * Derived from the supplier bills, NOT from Supplier.Balance — that column is a single
 * scalar with no currency, and the lab finance sync only moves it for YER bills, so a lab
 * that invoices in SAR or USD shows a balance of zero there no matter how much is owed.
 */
export interface LabAccount {
  supplierId: string;
  labName: string;
  openBills: number;
  billedByCurrency: LabCurrencyAmount[];
  paidByCurrency: LabCurrencyAmount[];
  balanceByCurrency: LabCurrencyAmount[];
}

/** CORE-LAB-015: lab performance thresholds, owner-configurable from Settings. */
export interface LabPerformanceThresholds {
  remakeRateAlarm: number;
  remakeRateWarn: number;
  overdueRateAlarm: number;
  overdueRateWarn: number;
  turnaroundDaysTarget: number;
  onTimeRateGood: number;
  onTimeRateWarn: number;
}

export interface LabDashboardData {
  kpis: LabDashboardKPIs;
  statusDistribution: StatusDistributionItem[];
  topLabs: TopLabItem[];
  recentOverdue: OverdueOrderItem[];
  monthlyTrend: MonthlyTrendItem[];
  labAccounts: LabAccount[];
}
