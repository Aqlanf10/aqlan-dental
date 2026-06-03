"use client";
import { useAuthStore } from "@/stores/authStore";

/**
 * Permission key mapping for the application.
 *
 * NAMING CONTRACT: All keys use the underscore format (e.g. "daily_operations.view")
 * to match the backend AuthController permission generation, which produces keys
 * from {resource}.{action} based on the RolePermissions table seeded by DbSeeder.
 *
 * Frontend daily operations actions are mapped to existing backend resources:
 *   checkIn / createWalkIn  → daily_operations.create
 *   callPatient             → clinic_queue.create
 *   recallPatient           → clinic_queue.edit
 *   enterRoom               → clinic_queue.approve
 *   changeRoom              → rooms.edit
 *   collectPayment           → finance.create
 *   createDraftInvoice       → invoices.create
 *   closeVisit               → visits.edit
 *   managerOverride          → settings.approve
 *   lab.view                 → daily_operations.view
 *   lab.manage               → daily_operations.edit
 *   reports.daily.view       → reports.view
 *   commissions.view         → finance.view
 *   settings.paymentMethods  → settings.edit (admin-only)
 */
export const PERMISSION_KEYS = {
  // Daily Operations — mapped to existing backend resources
  DAILY_OPERATIONS_VIEW: "daily_operations.view",
  DAILY_OPS_CHECK_IN: "daily_operations.create",        // check-in → daily_operations.create
  DAILY_OPS_CREATE_WALK_IN: "daily_operations.create",  // walk-in → daily_operations.create
  DAILY_OPS_CALL_PATIENT: "clinic_queue.create",        // call → clinic_queue.create
  DAILY_OPS_RECALL_PATIENT: "clinic_queue.edit",        // recall → clinic_queue.edit
  DAILY_OPS_ENTER_ROOM: "clinic_queue.approve",         // enter room → clinic_queue.approve
  DAILY_OPS_CHANGE_ROOM: "rooms.edit",                  // change room → rooms.edit
  DAILY_OPS_COLLECT_PAYMENT: "finance.create",          // collect payment → finance.create
  DAILY_OPS_CREATE_DRAFT_INVOICE: "invoices.create",    // draft invoice → invoices.create
  DAILY_OPS_CLOSE_VISIT: "visits.edit",                 // close visit → visits.edit
  DAILY_OPS_MANAGER_OVERRIDE: "settings.approve",       // manager override → settings.approve
  DAILY_OPS_LAB_VIEW: "lab_orders.view",            // lab view → lab_orders.view
  DAILY_OPS_LAB_MANAGE: "lab_orders.edit",          // lab manage → lab_orders.edit
  REPORTS_DAILY_VIEW: "reports.view",                   // daily report → reports.view
  COMMISSIONS_VIEW: "finance.view",                     // commissions → finance.view
  SETTINGS_PAYMENT_METHODS_MANAGE: "settings.edit",     // payment methods → settings.edit (admin-only)

  // Booking Requests
  BOOKING_REQUESTS_VIEW: "booking_requests.view",
  BOOKING_REQUESTS_CREATE: "booking_requests.create",
  BOOKING_REQUESTS_EDIT: "booking_requests.edit",

  // Appointments
  APPOINTMENTS_VIEW: "appointments.view",
  APPOINTMENTS_CREATE: "appointments.create",
  APPOINTMENTS_EDIT: "appointments.edit",
  APPOINTMENTS_EXPORT: "appointments.export",

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

  // Finance / Payments
  PAYMENTS_VIEW: "finance.view",
  PAYMENTS_CREATE: "finance.create",
  PAYMENTS_EDIT: "finance.edit",
  PAYMENTS_APPROVE: "finance.approve",
  PAYMENTS_DELETE: "finance.delete",
  PAYMENTS_EXPORT: "finance.export",

  // Invoices
  INVOICES_VIEW: "invoices.view",
  INVOICES_CREATE: "invoices.create",

  // Rooms
  ROOMS_VIEW: "rooms.view",
  ROOMS_CREATE: "rooms.create",
  ROOMS_EDIT: "rooms.edit",

  // Patients
  PATIENTS_VIEW: "patients.view",
  PATIENTS_CREATE: "patients.create",

  // Settings
  SETTINGS_VIEW: "settings.view",
  SETTINGS_EDIT: "settings.edit",
  SETTINGS_APPROVE: "settings.approve",

  // Reports
  REPORTS_VIEW: "reports.view",
  REPORTS_CREATE: "reports.create",
} as const;

export type PermissionKey = (typeof PERMISSION_KEYS)[keyof typeof PERMISSION_KEYS];

/**
 * Role-based fallback map for daily operations permissions.
 * When a user has no explicit permissions loaded from backend,
 * we infer from their role using the SAME underscore keys that
 * the backend AuthController generates.
 */
const ROLE_FALLBACK: Record<string, string[]> = {
  Admin: ["*"],
  Reception: [
    "daily_operations.view", "daily_operations.create",
    "clinic_queue.view", "clinic_queue.create", "clinic_queue.edit", "clinic_queue.approve",
    "appointments.view", "appointments.create", "appointments.edit",
    "finance.view", "finance.create",
    "patients.view", "patients.create",
    "lab_orders.view",
    "rooms.view", "rooms.create", "rooms.edit",
    "visits.view", "visits.edit",
    "checkout.view", "invoices.view", "invoices.create",
  ],
  Accountant: [
    "daily_operations.view",
    "finance.view", "finance.create", "finance.edit", "finance.export",
    "reports.view",
  ],
  Assistant: [
    "daily_operations.view",
    "clinic_queue.view", "clinic_queue.create",
    "lab_orders.view",
    "rooms.view", "rooms.edit",
  ],
  Orthodontist: ["daily_operations.view", "lab_orders.view", "lab_orders.edit"],
  GeneralDentist: ["daily_operations.view", "lab_orders.view"],
  OralSurgeon: ["daily_operations.view", "lab_orders.view"],
  BranchManager: [
    "daily_operations.view", "daily_operations.create", "daily_operations.edit",
    "clinic_queue.view", "clinic_queue.create", "clinic_queue.edit", "clinic_queue.approve",
    "finance.view", "finance.create",
    "lab_orders.view", "lab_orders.edit",
    "rooms.view", "rooms.create", "rooms.edit",
    "visits.view", "visits.edit",
    "invoices.view", "invoices.create",
    "checkout.view",
    "settings.view", "settings.approve",
    "reports.view",
  ],
};

/**
 * Hook to check if the current user has a specific permission.
 * Uses backend permission keys when available, falls back to role-based inference.
 * Admin always has all permissions.
 */
export function useHasPermission(permissionKey: string): boolean {
  const { user } = useAuthStore();

  // Admin always has all permissions
  if (user?.role === "Admin") return true;

  // Check backend permission keys if available
  if (user?.permissions && user.permissions.length > 0) {
    return user.permissions.includes(permissionKey);
  }

  // Fallback: role-based permission inference
  if (user?.role) {
    const rolePerms = ROLE_FALLBACK[user.role];
    if (rolePerms) {
      return rolePerms.includes("*") || rolePerms.includes(permissionKey);
    }
  }

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

  // Fallback to role-based
  if (user?.role) {
    const rolePerms = ROLE_FALLBACK[user.role];
    if (rolePerms) {
      return rolePerms.includes("*") || permissionKeys.some((key) => rolePerms.includes(key));
    }
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
  // Fallback to role
  if (user.role) {
    const rolePerms = ROLE_FALLBACK[user.role];
    if (rolePerms) {
      return rolePerms.includes("*") || rolePerms.includes(permissionKey);
    }
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
  // Fallback to role
  if (user.role) {
    const rolePerms = ROLE_FALLBACK[user.role];
    if (rolePerms) {
      return rolePerms.includes("*") || permissionKeys.some((key) => rolePerms.includes(key));
    }
  }
  return false;
}
