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
