import type { MobileLoginResponse, StaffUser, UserPermissions } from "./types";

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

function boolean(value: unknown, fallback: boolean): boolean {
  return typeof value === "boolean" ? value : fallback;
}

export function normalizeStaffUser(value: unknown): StaffUser | null {
  const source = record(value);
  if (!source) return null;
  const id = text(property(source, "id", "Id"));
  const username = text(property(source, "username", "Username"));
  if (!id || !username) return null;
  return {
    id,
    username,
    role: text(property(source, "role", "Role")) || "Staff",
    branchId: optionalText(property(source, "branchId", "BranchId")),
    doctorName: optionalText(property(source, "doctorName", "DoctorName")),
    doctorId: optionalText(property(source, "doctorId", "DoctorId")),
    doctorColor: optionalText(property(source, "doctorColor", "DoctorColor")),
    doctorInitials: optionalText(property(source, "doctorInitials", "DoctorInitials")),
    mustChangePassword: boolean(property(source, "mustChangePassword", "MustChangePassword"), false),
    email: optionalText(property(source, "email", "Email")),
    isActive: boolean(property(source, "isActive", "IsActive"), true),
    deletedAt: optionalText(property(source, "deletedAt", "DeletedAt"))
  };
}

export function normalizePermissions(value: unknown): UserPermissions {
  const source = record(value) ?? {};
  const raw = property(source, "permissions", "Permissions");
  const permissions = Array.isArray(raw)
    ? Array.from(new Set(raw.filter((item): item is string => typeof item === "string").map((item) => item.trim()).filter(Boolean)))
    : [];
  return {
    role: text(property(source, "role", "Role")) || "Staff",
    permissions
  };
}

export function normalizeMobileLoginResponse(value: unknown): MobileLoginResponse | null {
  const source = record(value);
  if (!source) return null;
  const accessToken = text(property(source, "accessToken", "AccessToken"));
  const refreshToken = text(property(source, "refreshToken", "RefreshToken"));
  const user = normalizeStaffUser(property(source, "user", "User"));
  if (!accessToken || !refreshToken || !user) return null;
  return { accessToken, refreshToken, user };
}
