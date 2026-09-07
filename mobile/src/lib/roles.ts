import type { StaffUser } from "@/lib/types";

const CLINICAL_ROLES = new Set(["Admin", "Orthodontist", "GeneralDentist", "OralSurgeon"]);

export function canAccessClinicalRecords(user?: StaffUser | null): boolean {
  return Boolean(user && CLINICAL_ROLES.has(user.role));
}

export function canWriteClinicalRecords(user?: StaffUser | null): boolean {
  // Mirrors the current backend ClinicalWrite policy. Kept separate from read on purpose
  // so the mobile UI does not couple future read-only clinical roles to mutation rights.
  return Boolean(user && CLINICAL_ROLES.has(user.role));
}
