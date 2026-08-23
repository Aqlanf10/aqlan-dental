import { ApiError, isRecord, readBoolean, readNumber, readString, type RequestOptions } from './client';
import type { ClinicRoom, DailyPatient } from '@/types/dailyOperations';

type AuthorizedRequest = (path: string, options?: RequestOptions) => Promise<unknown>;

function optionalString(value: unknown, key: string) {
  return readString(value, key);
}

function requiredString(value: unknown, key: string, label: string) {
  const result = readString(value, key);
  if (!result) throw new ApiError(`Invalid ${label}`, null, 'invalid-response');
  return result;
}

function unwrapArray(payload: unknown): unknown[] {
  if (Array.isArray(payload)) return payload;
  if (!isRecord(payload)) throw new ApiError('Invalid list payload', null, 'invalid-response');
  for (const key of ['items', 'data', 'results']) {
    if (Array.isArray(payload[key])) return payload[key];
  }
  throw new ApiError('Invalid list payload', null, 'invalid-response');
}

function parsePatient(value: unknown): DailyPatient {
  if (!isRecord(value)) throw new ApiError('Invalid patient payload', null, 'invalid-response');
  return {
    appointmentId: optionalString(value, 'appointmentId'),
    appointmentDate: optionalString(value, 'appointmentDate'),
    arrivedAt: optionalString(value, 'arrivedAt'),
    queueAddedAt: optionalString(value, 'queueAddedAt'),
    visitStartedAt: optionalString(value, 'visitStartedAt'),
    patientId: requiredString(value, 'patientId', 'patient identifier'),
    patientName: requiredString(value, 'patientName', 'patient name'),
    patientPhone: optionalString(value, 'patientPhone'),
    patientNumber: optionalString(value, 'patientNumber'),
    appointmentTime: optionalString(value, 'appointmentTime'),
    appointmentType: optionalString(value, 'appointmentType'),
    appointmentStatus: optionalString(value, 'appointmentStatus') ?? 'Scheduled',
    doctorId: optionalString(value, 'doctorId'),
    doctorName: optionalString(value, 'doctorName') ?? '—',
    serviceId: optionalString(value, 'serviceId'),
    serviceName: optionalString(value, 'serviceName'),
    roomName: optionalString(value, 'roomName'),
    roomId: optionalString(value, 'roomId'),
    queueItemId: optionalString(value, 'queueItemId'),
    queueStatus: optionalString(value, 'queueStatus'),
    visitId: optionalString(value, 'visitId'),
    visitStatus: optionalString(value, 'visitStatus'),
    proposedProcedure: optionalString(value, 'proposedProcedure'),
    consultationFeeRequired: readBoolean(value, 'consultationFeeRequired'),
    consultationFeePaid: readBoolean(value, 'consultationFeePaid'),
    paymentBeforeEntryRequired: readBoolean(value, 'paymentBeforeEntryRequired'),
    financialEntryStatus: optionalString(value, 'financialEntryStatus'),
    financialEntryReason: optionalString(value, 'financialEntryReason'),
    canEnterWithoutPayment: readBoolean(value, 'canEnterWithoutPayment', true),
    managerOverrideAllowed: readBoolean(value, 'managerOverrideAllowed'),
    hasMedicalAlerts: readBoolean(value, 'hasMedicalAlerts'),
    visitCount: readNumber(value, 'visitCount'),
    nextAction: optionalString(value, 'nextAction') ?? '',
  };
}

function parseRoom(value: unknown): ClinicRoom {
  return {
    id: requiredString(value, 'id', 'room identifier'),
    arabicName: requiredString(value, 'arabicName', 'room name'),
  };
}

function post(request: AuthorizedRequest, path: string, body?: Record<string, unknown>) {
  return request(path, {
    method: 'POST',
    body: body ? JSON.stringify(body) : undefined,
  });
}

export function createDailyOperationsApi(request: AuthorizedRequest) {
  return {
    async today(): Promise<DailyPatient[]> {
      return unwrapArray(await request('/api/patient-journey/today')).map(parsePatient);
    },

    async rooms(): Promise<ClinicRoom[]> {
      return unwrapArray(await request('/api/settings/rooms/active')).map(parseRoom);
    },

    intake(appointmentId: string, body: { roomId?: string; notes?: string; serviceId?: string }) {
      return post(request, `/api/patient-journey/${encodeURIComponent(appointmentId)}/intake`, body);
    },

    sendToQueue(appointmentId: string, body: { roomId?: string; notes?: string }) {
      return post(request, `/api/patient-journey/${encodeURIComponent(appointmentId)}/send-to-queue`, body);
    },

    call(queueItemId: string, roomName?: string) {
      return post(request, `/api/clinic-queue/${encodeURIComponent(queueItemId)}/call`, roomName ? { roomName } : undefined);
    },

    recall(queueItemId: string, roomName?: string) {
      return post(request, `/api/clinic-queue/${encodeURIComponent(queueItemId)}/recall`, roomName ? { roomName } : undefined);
    },

    enterRoom(queueItemId: string) {
      return post(request, `/api/clinic-queue/${encodeURIComponent(queueItemId)}/enter-room`);
    },

    startVisit(appointmentId: string) {
      return post(request, `/api/patient-journey/${encodeURIComponent(appointmentId)}/start-visit`);
    },
  };
}
