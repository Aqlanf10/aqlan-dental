"use client";
import { useAuthStore } from "@/stores/authStore";

/** Permission key mapping for daily operations module visibility */
export const PERMISSION_KEYS = {
  // Daily Operations group
  DAILY_OPERATIONS_VIEW: "daily_operations.view",

  // Booking Requests
  BOOKING_REQUESTS_VIEW: "booking_requests.view",
  BOOKING_REQUESTS_CREATE: "booking_requests.create",
  BOOKING_REQUESTS_EDIT: "booking_requests.edit",

  // Appointments
  APPOINTMENTS_VIEW: "appointments.view",
  APPOINTMENTS_CREATE: "appointments.create",
  APPOINTMENTS_EDIT: "appointments.edit",
  APPOINTMENTS_EXPORT: "appointments.export", // used as "today view" flag

  // Clinic Queue
  CLINIC_QUEUE_VIEW: "clinic_queue.view",
  CLINIC_QUEUE_CREATE: "clinic_queue.create",
  CLINIC_QUEUE_EDIT: "clinic_queue.edit",
  CLINIC_QUEUE_APPROVE: "clinic_queue.approve",

  // Clinic Display
  CLINIC_DISPLAY_VIEW: "clinic_display.view",

  // Patient Journey
  PATIENT_JOURNEY_VIEW: "patient_journey.view",
  PATIENT_JOURNEY_EDIT: "patient_journey.edit",

  // Visits
  VISITS_VIEW: "visits.view",
  VISITS_EDIT: "visits.edit",

  // Checkout
  CHECKOUT_VIEW: "checkout.view",

  // Payments (reuse existing finance resource)
  PAYMENTS_VIEW: "finance.view",
  PAYMENTS_CREATE: "finance.create",

  // Invoices
  INVOICES_VIEW: "invoices.view",
  INVOICES_CREATE: "invoices.create",

  // Rooms
  ROOMS_VIEW: "rooms.view",
  ROOMS_EDIT: "rooms.edit",

  // Patients
  PATIENTS_VIEW: "patients.view",
  PATIENTS_CREATE: "patients.create",
} as const;

export type PermissionKey = (typeof PERMISSION_KEYS)[keyof typeof PERMISSION_KEYS];

/**
 * Hook to check if the current user has a specific permission.
 * Falls back to role-based checking if permissions are not loaded.
 * Admin always has all permissions.
 */
export function useHasPermission(permissionKey: string): boolean {
  const { user } = useAuthStore();

  // Admin always has all permissions
  if (user?.role === "Admin") return true;

  // Check permission keys if available
  if (user?.permissions && user.permissions.length > 0) {
    return user.permissions.includes(permissionKey);
  }

  // Fallback: no permissions loaded, deny by default for safety
  // But allow basic access for authenticated users
  return false;
}

/**
 * Hook to check multiple permissions. Returns true if user has ANY of the given permissions.
 */
export function useHasAnyPermission(permissionKeys: string[]): boolean {
  const { user } = useAuthStore();

  if (user?.role === "Admin") return true;

  if (user?.permissions && user.permissions.length > 0) {
    return permissionKeys.some((key) => user.permissions!.includes(key));
  }

  return false;
}

/**
 * Check permission without hook (for use in non-component code).
 */
export function hasPermission(
  user: { role?: string; permissions?: string[] } | null,
  permissionKey: string
): boolean {
  if (!user) return false;
  if (user.role === "Admin") return true;
  if (user.permissions && user.permissions.length > 0) {
    return user.permissions.includes(permissionKey);
  }
  return false;
}

/**
 * Check if user has any of the given permissions (non-hook version).
 */
export function hasAnyPermission(
  user: { role?: string; permissions?: string[] } | null,
  permissionKeys: string[]
): boolean {
  if (!user) return false;
  if (user.role === "Admin") return true;
  if (user.permissions && user.permissions.length > 0) {
    return permissionKeys.some((key) => user.permissions!.includes(key));
  }
  return false;
}
