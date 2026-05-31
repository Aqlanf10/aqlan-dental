export type CommissionStatus = "Pending" | "Calculated" | "Approved" | "Paid";
export type CommissionBaseRule = "GrossAmount" | "AfterDiscount" | "AfterDiscountAndCosts";
export type MaterialCostType = "FixedAmount" | "PercentageOfServicePrice";

export interface LineItemCommission {
  lineItemId: string;
  invoiceId: string;
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
  summary: CommissionReportSummary;
  rows: CommissionReportRow[];
}

export interface DoctorCommissionPayment {
  id: string;
  doctorId: string;
  doctorName: string | null;
  amount: number;
  paymentDate: string;
  paymentMethod: string | null;
  referenceNumber: string | null;
  notes: string | null;
  createdAt: string;
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
