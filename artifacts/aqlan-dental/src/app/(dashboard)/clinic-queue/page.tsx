import { redirect } from "@/lib/nextNavCompat";
import { getStaticRouteAlias } from "@/lib/canonicalRoutes";

/**
 * NAV-CEPH-FIX (audit §6 + §8) — Redirect stub.
 *
 * The old 1,064-line `/clinic-queue` page duplicated the queue UI that already
 * lives inside `/daily-operations?tab=queue`. The audit (rows 4.4 + 4.5) called
 * for consolidating the three parallel "today's patients" screens
 * (`/daily-operations`, `/clinic-queue`, `/patient-journey`) into one canonical
 * workspace.
 *
 * This file is now a thin server-component redirect to the canonical workspace
 * with the queue tab preselected. The underlying clinic-queue backend API
 * (`/api/clinic-queue/*`) is unchanged — `/daily-operations` consumes it
 * directly via ClinicQueueView.
 *
 * Direct URLs to `/clinic-queue` continue to work (they land here and redirect).
 */
export default function ClinicQueueRedirectPage() {
  redirect(getStaticRouteAlias("/clinic-queue").destination);
}
