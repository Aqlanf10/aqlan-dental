export interface LabOrder {
  id: string;
  orderNumber: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  orthoCaseNumber?: string;
  applianceType: string;
  labName?: string;
  sentDate?: string;
  expectedDate?: string;
  receivedDate?: string;
  status: LabOrderStatus;
  priority: LabOrderPriority;
  instructions?: string;
  cost?: number;
  doctorName?: string;
  createdAt: string;
}

export type LabOrderStatus = "sent" | "manufacturing" | "ready" | "received" | "cancelled";
export type LabOrderPriority = "urgent" | "normal" | "low";

export interface CreateLabOrderRequest {
  patientId: string;
  orthoCaseId?: string;
  applianceType: string;
  labName?: string;
  sentDate?: string;
  expectedDate?: string;
  priority?: LabOrderPriority;
  instructions?: string;
  cost?: number;
  doctorId?: string;
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
