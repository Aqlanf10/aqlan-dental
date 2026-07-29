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
  orthoCaseId?: string;
  roomName?: string | null;
  // Queue / clinic-flow fields (Sprint 4.5)
  arrivedAt?: string | null;
  calledAt?: string | null;
  inRoomAt?: string | null;
  // Email reminder availability
  hasEmail?: boolean;

  // YOLO-S1: Companion/Guardian + Color + Treatment Package — all optional,
  // nullable, default undefined so existing callers see no behavior change.
  companionName?: string | null;
  companionPhone?: string | null;
  companionRelationship?: string | null;
  appointmentColor?: string | null;
  packageId?: string | null;
  packageName?: string | null;
  packageColor?: string | null;
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
  orthoCaseId?: string;

  // YOLO-S1
  companionName?: string | null;
  companionPhone?: string | null;
  companionRelationship?: string | null;
  appointmentColor?: string | null;
  packageId?: string | null;
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

// ── YOLO-S1: Treatment Package types ─────────────────────────────────────────

export interface TreatmentPackageService {
  id: string;
  packageId: string;
  clinicServiceId: string;
  serviceArabicName: string;
  serviceEnglishName?: string | null;
  serviceCode?: string | null;
  serviceColor?: string | null;
  quantity: number;
  overridePrice?: number | null;
  /** Effective unit price = overridePrice ?? service default price. Computed server-side. */
  effectiveUnitPrice: number;
  /** Line total = effectiveUnitPrice * quantity. Computed server-side. */
  lineTotal: number;
  createdAt: string;
  updatedAt: string;
}

export interface TreatmentPackage {
  id: string;
  name: string;
  description?: string | null;
  totalPrice: number;
  sessionCount: number;
  color?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  /** YOLO-S2: services included in this package. Populated on GetById; empty on list. */
  services?: TreatmentPackageService[];
  /** YOLO-S2: computed catalog total = sum(lineTotal). Populated on GetById. */
  computedTotal?: number | null;
}

export interface CreateTreatmentPackageRequest {
  name: string;
  description?: string | null;
  totalPrice: number;
  sessionCount: number;
  color?: string | null;
  isActive: boolean;
}

export interface UpdateTreatmentPackageRequest {
  name: string;
  description?: string | null;
  totalPrice: number;
  sessionCount: number;
  color?: string | null;
  isActive?: boolean | null;
}

/** YOLO-S2: body for POST/PUT /api/treatment-packages/{id}/services[/{serviceId}] */
export interface UpsertPackageServiceRequest {
  clinicServiceId: string;
  quantity: number;
  overridePrice?: number | null;
}

/** YOLO-S2: DTO returned by GET /api/service-consumables?serviceId=... */
export interface ServiceConsumable {
  id: string;
  clinicServiceId: string;
  inventoryItemId: string;
  inventoryItemName: string;
  inventoryItemUnit?: string | null;
  quantity: number;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
}

/** YOLO-S2: body for POST /api/service-consumables */
export interface CreateServiceConsumableRequest {
  clinicServiceId: string;
  inventoryItemId: string;
  quantity: number;
  notes?: string | null;
}
