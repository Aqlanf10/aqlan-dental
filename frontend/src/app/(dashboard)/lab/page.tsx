"use client";

import { Suspense } from "react";
import { LabWorkspaceShell, useLabView } from "./_panels/LabWorkspaceShell";
import { LabOrdersPanel } from "./_panels/OrdersPanel";
import { LabOverduePanel } from "./_panels/OverduePanel";
import { LabPayablesPanel } from "./_panels/PayablesPanel";
import { LabReportsPanel } from "./_panels/ReportsPanel";
import { LabOverviewPanel } from "./_panels/OverviewPanel";
import { TableSkeleton } from "@/components/ui/skeleton";

/**
 * CORE-LAB-017 — the single lab workspace. `/lab/dashboard`, `/lab/overdue`, `/lab/reports`
 * and `/lab/payables` now redirect here with `?view=`, so existing links and bookmarks still
 * land on the right pivot instead of 404ing.
 */
function LabWorkspace() {
  const [view, setView] = useLabView();

  return (
    <LabWorkspaceShell active={view} onChange={setView}>
      {/* Mounted one at a time on purpose: each panel owns heavy queries, and keeping the
          other four mounted would fire four background refetches on every visit. */}
      {view === "orders"   && <LabOrdersPanel />}
      {view === "overdue"  && <LabOverduePanel />}
      {view === "payables" && <LabPayablesPanel />}
      {view === "reports"  && <LabReportsPanel />}
      {view === "overview" && <LabOverviewPanel />}
    </LabWorkspaceShell>
  );
}

export default function LabPage() {
  // useSearchParams needs a Suspense boundary for static prerendering.
  return (
    <Suspense fallback={<div className="p-6"><TableSkeleton rows={6} /></div>}>
      <LabWorkspace />
    </Suspense>
  );
}
