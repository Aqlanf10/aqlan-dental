/**
 * Shared role helpers for Aqlan Dental Pro
 *
 * Clinical roles are any practitioner roles that should be treated
 * as "Doctor" in the UI — they receive limited access to financial
 * data and admin-only workflows.
 */

/** All clinical roles that map to "Doctor / limited" in the UI. */
export const CLINICAL_ROLES = [
  "Doctor",
  "Orthodontist",
  "GeneralDentist",
  "OralSurgeon",
] as const;

export type ClinicalRole = (typeof CLINICAL_ROLES)[number];

/**
 * Returns true if the role string represents any clinical practitioner role.
 * Comparison is case-insensitive to tolerate API inconsistencies.
 */
export function isClinicalRole(role: string | null | undefined): boolean {
  if (!role) return false;
  const lower = role.toLowerCase();
  return CLINICAL_ROLES.some((r) => r.toLowerCase() === lower);
}

/** Convenience alias — same as isClinicalRole. */
export const isDoctorRole = isClinicalRole;

/** Returns true if the role is Accountant (case-insensitive). */
export function isAccountantRole(role: string | null | undefined): boolean {
  return role?.toLowerCase() === "accountant";
}

/** Returns true if the role is Admin (case-insensitive). */
export function isAdminRole(role: string | null | undefined): boolean {
  return role?.toLowerCase() === "admin";
}

/** Clinical records may be read only by an administrator or a practitioner. */
export function canReadClinical(role: string | null | undefined): boolean {
  return isAdminRole(role) || isClinicalRole(role);
}

/**
 * Clinical mutations currently use the same role set as clinical reads.
 * Keep this as a separate capability so a future read-only practitioner role
 * cannot accidentally gain write controls in the UI.
 */
export function canWriteClinical(role: string | null | undefined): boolean {
  return isAdminRole(role) || isClinicalRole(role);
}

/** Returns true if the role is Reception (case-insensitive). */
export function isReceptionRole(role: string | null | undefined): boolean {
  return role?.toLowerCase() === "reception";
}

/** Patient balance / outstanding — Admin and Accountant only. */
export function canViewPatientFinance(role: string | null | undefined): boolean {
  return isAdminRole(role) || isAccountantRole(role) || isReceptionRole(role);
}

/** Finance V3 reports / deep KPIs — Admin and Accountant only. */
export function canViewFinanceReports(role: string | null | undefined): boolean {
  return isAdminRole(role) || isAccountantRole(role);
}

/** Daily checkout / cashier — Admin, Reception, Accountant. */
export function canAccessCashier(role: string | null | undefined): boolean {
  if (!role) return false;
  const r = role.toLowerCase();
  return r === "admin" || r === "reception" || r === "accountant";
}
