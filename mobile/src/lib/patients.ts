import type { PaginatedResponse, PatientListItem } from "./types";

type UnknownRecord = Record<string, unknown>;

function record(value: unknown): UnknownRecord | null {
  return value !== null && typeof value === "object" ? value as UnknownRecord : null;
}

function text(value: unknown): string {
  return typeof value === "string" ? value.trim() : "";
}

function optionalText(value: unknown): string | null {
  const normalized = text(value);
  return normalized || null;
}

function finiteNumber(value: unknown): number | null {
  const normalized = typeof value === "number" ? value : Number(value);
  return Number.isFinite(normalized) ? normalized : null;
}

function boolean(value: unknown, fallback: boolean): boolean {
  return typeof value === "boolean" ? value : fallback;
}

function property(source: UnknownRecord, camel: string, pascal: string): unknown {
  return source[camel] ?? source[pascal];
}

export function normalizePatientListItem(value: unknown): PatientListItem | null {
  const source = record(value);
  if (!source) return null;

  const id = text(property(source, "id", "Id"));
  if (!id) return null;

  return {
    id,
    patientNumber: text(property(source, "patientNumber", "PatientNumber")) || "—",
    fullName: text(property(source, "fullName", "FullName")) || "مريض بدون اسم",
    phone: optionalText(property(source, "phone", "Phone")),
    email: optionalText(property(source, "email", "Email")),
    gender: optionalText(property(source, "gender", "Gender")),
    age: finiteNumber(property(source, "age", "Age")),
    primaryDoctorName: optionalText(property(source, "primaryDoctorName", "PrimaryDoctorName")),
    branchName: optionalText(property(source, "branchName", "BranchName")),
    createdAt: text(property(source, "createdAt", "CreatedAt")),
    isActive: boolean(property(source, "isActive", "IsActive"), true),
    lastVisitDate: optionalText(property(source, "lastVisitDate", "LastVisitDate"))
  };
}

export function normalizePatientPage(value: unknown): PaginatedResponse<PatientListItem> {
  const source = record(value) ?? {};
  const rawData = property(source, "data", "Data");
  const data = Array.isArray(rawData)
    ? rawData.map(normalizePatientListItem).filter((item): item is PatientListItem => item !== null)
    : [];
  const totalCount = finiteNumber(property(source, "totalCount", "TotalCount")) ?? data.length;
  const page = finiteNumber(property(source, "page", "Page")) ?? 1;
  const pageSize = finiteNumber(property(source, "pageSize", "PageSize")) ?? Math.max(data.length, 1);
  const totalPages = finiteNumber(property(source, "totalPages", "TotalPages"));

  return {
    data,
    totalCount: Math.max(0, totalCount),
    page: Math.max(1, page),
    pageSize: Math.max(1, pageSize),
    ...(totalPages === null ? {} : { totalPages: Math.max(0, totalPages) })
  };
}

export function patientInitial(name: unknown): string {
  return text(name).charAt(0) || "م";
}
