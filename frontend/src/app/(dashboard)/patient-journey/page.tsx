import { redirect } from "next/navigation";

/**
 * NAV-CEPH-FIX (audit §6 + §8) — Redirect stub.
 *
 * The old 1,472-line `/patient-journey` index page duplicated today's-patient
 * flow UI that already lives inside `/daily-operations`. The audit (row 4.5)
 * called for consolidating the three parallel "today's patients" screens
 * (`/daily-operations`, `/clinic-queue`, `/patient-journey`) into one canonical
 * workspace.
 *
 * This file is now a thin server-component redirect to the canonical workspace.
 * The `usePatientJourney` hook (`src/hooks/usePatientJourney.ts`) is unchanged
 * and still consumed by `/daily-operations` and other pages.
 *
 * `/patient-journey/[patientId]` (a separate route) already redirects to
 * `/patients/[patientId]?focus=journey` — that route is untouched here.
 *
 * Direct URLs to `/patient-journey` continue to work (they land here and
 * redirect).
 */
export default function PatientJourneyRedirectPage() {
  redirect("/daily-operations");
}
