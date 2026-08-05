export type CommissionStatus = "Pending" | "Calculated" | "Approved" | "Paid";
export type CommissionBaseRule = "GrossAmount" | "AfterDiscount" | "AfterDiscountAndCosts";
export type MaterialCostType = "FixedAmount" | "PercentageOfServicePrice";

export interface LineItemCommission {
  lineItemId: string;
  invoiceId: string;
  branchId: string;
  currency: "YER" | "SAR" | "USD";
  invoiceNumber: string;
  patientName: string;
  serviceName: string;
  doctorId: string | null;
  doctorName: string | null;
  totalPrice: number;
  lineDiscountAmount: number;
  materialCost: number;
  labCost: number;
  otherDirectCost: number;
  netCommissionableAmount: number;
  doctorCommissionPercentage: number;
  doctorCommissionAmount: number;
  centerShareAmount: number;
  commissionStatus: CommissionStatus;
  commissionNotes: string | null;
  hasLabOrder: boolean;
  labCostMissing: boolean;
  isApproved: boolean;
  commissionApprovedAt: string | null;
  createdAt: string;
}

export interface UpdateLineItemCommissionRequest {
  materialCost?: number;
  labCost?: number;
  otherDirectCost?: number;
  doctorCommissionPercentage?: number;
  commissionBaseRule?: CommissionBaseRule;
  doctorId?: string;
  commissionNotes?: string;
}

export interface CommissionReportRow {
  branchId: string;
  currency: "YER" | "SAR" | "USD";
  date: string;
  patientName: string;
  invoiceNumber: string;
  serviceName: string;
  doctorName: string | null;
  grossAmount: number;
  discount: number;
  materialCost: number;
  labCost: number;
  otherCosts: number;
  netCommissionableAmount: number;
  doctorPercentage: number;
  doctorCommission: number;
  paidCommission: number;
  remainingCommission: number;
  status: CommissionStatus;
}

export interface CommissionReportSummary {
  branchId: string;
  currency: "YER" | "SAR" | "USD";
  totalGross: number;
  totalDiscount: number;
  totalMaterialCost: number;
  totalLabCost: number;
  totalOtherCosts: number;
  totalNet: number;
  totalDoctorCommission: number;
  totalPaid: number;
  totalRemaining: number;
}

export interface CommissionReport {
  summaries: CommissionReportSummary[];
  rows: CommissionReportRow[];
}

export interface DoctorCommissionPayment {
  id: string;
  doctorId: string;
  branchId: string;
  currency: "YER" | "SAR" | "USD";
  doctorName: string | null;
  amount: number;
  paymentDate: string;
  paymentMethod: string | null;
  referenceNumber: string | null;
  notes: string | null;
  createdAt: string;
}

// ── Commission adjustments (CORE-FIN-LAB-ADJ) ────────────────────────────────

export type CommissionAdjustmentStatus = "Pending" | "Settled" | "Cancelled";

/**
 * A signed correction on an already-PAID commission. Positive means the clinic still owes the
 * doctor; negative means the doctor was overpaid and it comes off the next settlement. The
 * original commission line is never rewritten — this is how the difference is carried instead.
 */
export interface CommissionAdjustment {
  id: string;
  doctorId: string;
  branchId: string;
  currency: "YER" | "SAR" | "USD";
  doctorName: string | null;
  invoiceLineItemId: string;
  invoiceId: string;
  invoiceNumber: string;
  patientName: string;
  serviceName: string;
  labOrderId: string | null;
  labOrderNumber: string | null;
  paidCommissionAmount: number;
  recalculatedCommissionAmount: number;
  adjustmentAmount: number;
  previousLabCost: number;
  currentLabCost: number;
  reason: string;
  status: CommissionAdjustmentStatus;
  settledByPaymentId: string | null;
  settledOn: string | null;
  createdAt: string;
}

export interface CommissionResyncResult {
  lineItemsExamined: number;
  recalculated: number;
  adjustmentsRaised: number;
  netAdjustmentAmount: number;
  /** Records the sweep deliberately left alone — never silently dropped. */
  skipped: string[];
}

export interface DoctorSettlementSummary {
  doctorId: string;
  branchId: string;
  currency: "YER" | "SAR" | "USD";
  doctorName: string | null;
  earnedFromLineItems: number;
  adjustmentsTotal: number;
  totalDue: number;
  alreadyPaid: number;
  remaining: number;
  pendingAdjustmentCount: number;
}

export interface ServiceCommissionDefaults {
  serviceId: string;
  defaultMaterialCost: number;
  defaultMaterialCostType: MaterialCostType;
  defaultLabCost: number;
  defaultDoctorCommissionPercentage: number | null;
  commissionBaseRule: CommissionBaseRule;
  commissionRecognitionMode: "OnInvoiceApproval" | "OnPaymentCollection";
}
