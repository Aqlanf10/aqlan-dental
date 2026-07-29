import { redirect } from "@/lib/nextNavCompat";
import { buildPatientJourneyDestination } from "@/lib/canonicalRoutes";

/**
 * FE-06 — Redirect stub.
 *
 * The old 1734-line `/patient-journey/[patientId]` page duplicated patient
 * detail UI that already exists on `/patients/[id]` (banner, today's
 * appointment, finance snapshot, recent visits, timeline, etc.). The audit
 * (row 4.4) called for merging the three parallel patient-detail screens
 * into one canonical profile.
 *
 * This file is now a thin server-component redirect that preserves the
 * patient ID and forwards any remaining search params (e.g. `?tab=...`)
 * to the canonical patient profile with `?focus=journey` so the receiving
 * page activates the most journey-relevant tab.
 *
 * Journey workflow actions (intake, send-to-queue, call patient, enter
 * room, start visit, handoff, no-show, cancel) remain reachable via
 * `/daily-operations` and `/patient-journey` (the index page), which share
 * the same `usePatientJourney` hooks.
 */
export default async function PatientJourneyRedirectPage({
  params,
  searchParams,
}: {
  params: Promise<{ patientId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { patientId } = await params;
  const sp = await searchParams;
  redirect(buildPatientJourneyDestination(patientId, sp));
}
