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

export type LabOrderStatus = "draft" | "sent" | "manufacturing" | "ready" | "received" | "delivered" | "returned" | "remake" | "cancelled";
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
  labOrderId: string;
  labName?: string;
  orderNumber?: string;
  patientName?: string;
  amount: number;
  paidAmount: number;
  balance: number;
  status: "pending" | "partial" | "paid";
  dueDate?: string;
  notes?: string;
  createdAt: string;
  updatedAt: string;
}

export interface RecordLabPaymentRequest {
  amount: number;
  notes?: string;
}
