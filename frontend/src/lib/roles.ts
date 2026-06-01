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

/** Returns true if the role is Reception (case-insensitive). */
export function isReceptionRole(role: string | null | undefined): boolean {
  return role?.toLowerCase() === "reception";
}
