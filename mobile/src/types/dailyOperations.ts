export type DailyPatient = {
  appointmentId: string | null;
  appointmentDate: string | null;
  arrivedAt: string | null;
  queueAddedAt: string | null;
  visitStartedAt: string | null;
  patientId: string;
  patientName: string;
  patientPhone: string | null;
  patientNumber: string | null;
  appointmentTime: string | null;
  appointmentType: string | null;
  appointmentStatus: string;
  doctorId: string | null;
  doctorName: string;
  serviceId: string | null;
  serviceName: string | null;
  roomName: string | null;
  roomId: string | null;
  queueItemId: string | null;
  queueStatus: string | null;
  visitId: string | null;
  visitStatus: string | null;
  proposedProcedure: string | null;
  hasMedicalAlerts: boolean;
  visitCount: number | null;
  nextAction: string;
};

export type ClinicRoom = {
  id: string;
  arabicName: string;
};

export type DailyOperationAction = 'call' | 'recall' | 'enter-room' | 'start-visit';
