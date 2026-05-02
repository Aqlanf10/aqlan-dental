export interface Appointment {
  id: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorId: string;
  doctorName: string;
  doctorColor?: string;
  appointmentDate: string;
  startTime: string;
  endTime: string;
  durationMinutes: number;
  appointmentType: string;
  specialty?: string;
  status: string;
  notes?: string;
  // Queue / clinic-flow fields (Sprint 4.5)
  roomName?: string | null;
  arrivedAt?: string | null;
  calledAt?: string | null;
  inRoomAt?: string | null;
}

export interface CreateAppointmentRequest {
  patientId: string;
  doctorId: string;
  appointmentDate: string;
  startTime: string;
  durationMinutes: number;
  appointmentType: string;
  specialty?: string;
  notes?: string;
}

export interface QueueItem {
  appointmentId: string;
  patientId: string;
  patientName: string;
  patientNumber: string;
  doctorName: string;
  appointmentTime: string;
  status: string;
  roomName?: string | null;
  arrivedAt?: string | null;
  calledAt?: string | null;
  inRoomAt?: string | null;
}

export interface QueueDisplay {
  latestCalled: {
    patientId: string;
    patientNumber: string;
    patientName: string;
    doctorName: string;
    roomName: string;
    calledAt: string;
  } | null;
  waitingCount: number;
  waitingList: {
    patientId: string;
    patientNumber: string;
    patientName: string;
    appointmentTime: string;
    doctorName: string;
    status: string;
  }[];
  recentlyCalled: {
    patientId: string;
    patientNumber: string;
    patientName: string;
    doctorName: string;
    roomName: string;
    status: string;
    calledAt: string;
  }[];
}
