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
  // Service / Room linking (Sprint 5)
  serviceId?: string;
  serviceName?: string;
  clinicRoomId?: string;
  roomName?: string | null;
  // Queue / clinic-flow fields (Sprint 4.5)
  arrivedAt?: string | null;
  calledAt?: string | null;
  inRoomAt?: string | null;
  // Email reminder availability
  hasEmail?: boolean;
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
  serviceId?: string;
  clinicRoomId?: string;
}

export interface BatchUpdateStatusRequest {
  appointmentIds: string[];
  status: string;
}

export interface UpcomingAppointment {
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
  status: string;
  serviceName?: string;
  roomName?: string;
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
