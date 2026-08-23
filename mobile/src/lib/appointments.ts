import type { Appointment, DoctorSummary } from "./types";

type UnknownRecord = Record<string, unknown>;

function record(value: unknown): UnknownRecord | null {
  return value !== null && typeof value === "object" ? value as UnknownRecord : null;
}

function property(source: UnknownRecord, camel: string, pascal: string): unknown {
  return source[camel] ?? source[pascal];
}

function text(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function optionalText(value: unknown): string | null {
  const normalized = text(value);
  return normalized || null;
}

function finiteNumber(value: unknown, fallback = 0): number {
  const normalized = typeof value === "number" ? value : Number(value);
  return Number.isFinite(normalized) ? normalized : fallback;
}

function boolean(value: unknown, fallback: boolean): boolean {
  return typeof value === "boolean" ? value : fallback;
}

export function normalizeAppointment(value: unknown): Appointment | null {
  const source = record(value);
  if (!source) return null;

  const id = text(property(source, "id", "Id"));
  const patientId = text(property(source, "patientId", "PatientId"));
  if (!id || !patientId) return null;

  return {
    id,
    patientId,
    patientName: text(property(source, "patientName", "PatientName")) || "مريض بدون اسم",
    patientNumber: text(property(source, "patientNumber", "PatientNumber")) || "—",
    doctorId: text(property(source, "doctorId", "DoctorId")),
    doctorName: text(property(source, "doctorName", "DoctorName")) || "غير محدد",
    doctorColor: optionalText(property(source, "doctorColor", "DoctorColor")),
    appointmentDate: text(property(source, "appointmentDate", "AppointmentDate")),
    startTime: text(property(source, "startTime", "StartTime")) || "—",
    endTime: text(property(source, "endTime", "EndTime")) || "—",
    durationMinutes: Math.max(0, finiteNumber(property(source, "durationMinutes", "DurationMinutes"))),
    appointmentType: text(property(source, "appointmentType", "AppointmentType")) || "موعد",
    specialty: optionalText(property(source, "specialty", "Specialty")),
    status: text(property(source, "status", "Status")) || "Scheduled",
    notes: optionalText(property(source, "notes", "Notes")),
    roomName: optionalText(property(source, "roomName", "RoomName")),
    arrivedAt: optionalText(property(source, "arrivedAt", "ArrivedAt")),
    calledAt: optionalText(property(source, "calledAt", "CalledAt")),
    inRoomAt: optionalText(property(source, "inRoomAt", "InRoomAt")),
    packageName: optionalText(property(source, "packageName", "PackageName"))
  };
}

export function normalizeAppointmentList(value: unknown): Appointment[] {
  const source = record(value);
  const rawItems = Array.isArray(value)
    ? value
    : source && Array.isArray(property(source, "data", "Data"))
      ? property(source, "data", "Data") as unknown[]
      : [];

  return rawItems
    .map(normalizeAppointment)
    .filter((item): item is Appointment => item !== null);
}

export function normalizeDoctor(value: unknown): DoctorSummary | null {
  const source = record(value);
  if (!source) return null;

  const id = text(property(source, "id", "Id"));
  const name = text(property(source, "name", "Name"));
  if (!id || !name) return null;

  return {
    id,
    name,
    specialty: optionalText(property(source, "specialty", "Specialty")),
    color: optionalText(property(source, "color", "Color")),
    branchId: optionalText(property(source, "branchId", "BranchId")),
    branchName: optionalText(property(source, "branchName", "BranchName")),
    isActive: boolean(property(source, "isActive", "IsActive"), true),
    defaultClinicRoomId: optionalText(property(source, "defaultClinicRoomId", "DefaultClinicRoomId")),
    defaultRoomName: optionalText(property(source, "defaultRoomName", "DefaultRoomName"))
  };
}

export function normalizeDoctorList(value: unknown): DoctorSummary[] {
  if (!Array.isArray(value)) return [];
  return value.map(normalizeDoctor).filter((item): item is DoctorSummary => item !== null);
}
