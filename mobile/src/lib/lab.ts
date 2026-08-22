export const LAB_STATUS_LABELS: Record<string, string> = {
  draft: "مسودة",
  sent: "مرسل",
  manufacturing: "قيد التصنيع",
  tryIn: "تجربة",
  ready: "جاهز",
  received: "مستلم من المعمل",
  delivered: "مسلّم للمريض",
  returned: "مرتجع",
  remake: "إعادة تصنيع",
  cancelled: "ملغي"
};

export const LAB_PRIORITY_OPTIONS = [
  { label: "عاجل", value: "urgent" },
  { label: "عادي", value: "normal" },
  { label: "منخفض", value: "low" }
] as const;

export const LAB_PRIORITY_LABELS: Record<string, string> = {
  urgent: "عاجل",
  normal: "عادي",
  low: "منخفض"
};

export type LabEntity = {
  id: string;
  name: string;
  phone?: string | null;
  whatsApp?: string | null;
  address?: string | null;
  contactPerson?: string | null;
  branchId?: string | null;
  isActive: boolean;
};

export type LabOrderListItem = {
  id: string;
  orderNumber?: string | null;
  patientId: string;
  patientName: string;
  patientNumber?: string | null;
  orthoCaseNumber?: string | null;
  applianceType?: string | null;
  labName?: string | null;
  labEntityName?: string | null;
  labId?: string | null;
  sentDate?: string | null;
  expectedDate?: string | null;
  receivedDate?: string | null;
  deliveredDate?: string | null;
  status: string;
  priority: string;
  cost?: number | null;
  totalCost?: number | null;
  currency?: string | null;
  exchangeRateToYer?: number | null;
  doctorName?: string | null;
  shade?: string | null;
  restorationType?: string | null;
  visitId?: string | null;
  cancellationReason?: string | null;
  createdAt: string;
};

export type LabOrderListResponse = {
  data: LabOrderListItem[];
  total: number;
  page: number;
  pageSize: number;
};

export type LabOrderItem = {
  id: string;
  workTypeId: string;
  workTypeName?: string | null;
  toothNumber?: string | null;
  arch?: string | null;
  shade?: string | null;
  restorationType?: string | null;
  unitsCount: number;
  unitPrice?: number | null;
  totalPrice?: number | null;
  instructions?: string | null;
  sortOrder: number;
};

export type LabOrderDetail = {
  id: string;
  orderNumber?: string | null;
  patientId: string;
  patientName: string;
  patientNumber?: string | null;
  orthoCaseNumber?: string | null;
  applianceType?: string | null;
  labName?: string | null;
  labEntityName?: string | null;
  labId?: string | null;
  sentDate?: string | null;
  expectedDate?: string | null;
  receivedDate?: string | null;
  deliveredDate?: string | null;
  status: string;
  priority: string;
  instructions?: string | null;
  cost?: number | null;
  totalCost?: number | null;
  doctorName?: string | null;
  shade?: string | null;
  restorationType?: string | null;
  visitId?: string | null;
  cancellationReason?: string | null;
  createdAt: string;
  items: LabOrderItem[];
};

export type LabOrderHistory = {
  id: string;
  fromStatus: string;
  toStatus: string;
  changedByName?: string | null;
  reason?: string | null;
  createdAt: string;
};

export type CreateLabOrderInput = {
  patientId: string;
  orthoCaseId?: string | null;
  applianceType: string;
  labName?: string | null;
  labId?: string | null;
  sentDate?: string | null;
  expectedDate?: string | null;
  priority: "urgent" | "normal" | "low";
  instructions?: string | null;
  cost?: number | null;
  currency: "YER" | "SAR" | "USD";
  exchangeRateToYer?: number | null;
  doctorId?: string | null;
  shade?: string | null;
  restorationType?: string | null;
  visitId?: string | null;
};

export function labStatusLabel(value?: string | null): string {
  if (!value) return "—";
  const canonical = value.toLowerCase() === "tryin" ? "tryIn" : value;
  return LAB_STATUS_LABELS[canonical] ?? value;
}

export function labStatusTransitions(status?: string | null): string[] {
  const current = status === "tryin" ? "tryIn" : status;
  switch (current) {
    case "draft": return ["sent", "cancelled"];
    case "sent": return ["manufacturing", "cancelled"];
    case "manufacturing": return ["tryIn", "ready", "cancelled"];
    case "tryIn": return ["ready", "returned", "cancelled"];
    case "ready": return ["received", "returned", "cancelled"];
    case "received": return ["delivered", "returned"];
    case "returned": return ["remake", "cancelled"];
    case "remake": return ["sent", "cancelled"];
    default: return [];
  }
}

export function formatMoney(amount?: number | null, currency?: string | null): string {
  if (amount == null) return "—";
  return `${amount.toLocaleString()} ${currency || ""}`.trim();
}
