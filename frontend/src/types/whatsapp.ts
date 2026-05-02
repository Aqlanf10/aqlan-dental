export interface WhatsAppMessage {
  id: string;
  patientId: string;
  patientName: string;
  phoneNumber: string;
  templateType: string;
  messageContent: string;
  status: string;
  errorMessage?: string;
  retryCount: number;
  sentAt?: string;
  createdAt: string;
  relatedEntityId?: string;
  relatedEntityType?: string;
}

export interface WhatsAppTemplate {
  id: string;
  templateKey: string;
  nameAr: string;
  contentTemplate: string;
  isActive: boolean;
  category: string;
}

export interface WhatsAppDashboard {
  sentToday: number;
  deliveredToday: number;
  failedToday: number;
  pendingCount: number;
  recentMessages: WhatsAppMessage[];
}

export interface SendMessageRequest {
  patientId: string;
  templateType: string;
  parameters?: Record<string, string>;
  customMessage?: string;
}

export interface SendAppointmentReminderRequest {
  appointmentId: string;
  hoursBefore?: number;
}
