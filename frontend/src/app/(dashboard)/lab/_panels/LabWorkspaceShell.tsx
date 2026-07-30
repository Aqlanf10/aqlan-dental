"use client";

import React from "react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  FlaskConical, ClipboardList, Clock, DollarSign, BarChart2, LayoutDashboard,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useHasPermission, PERMISSION_KEYS } from "@/hooks/usePermissions";

/**
 * CORE-LAB-017 — one lab workspace instead of five sidebar entries.
 *
 * The lab is a single job: an order is raised, chased while it is late, paid for, and then
 * looked at in aggregate. Splitting that across five routes meant the receptionist chasing a
 * late crown had to leave the screen to see whether the lab had been paid, and the sidebar
 * carried five links for one module. This is the Office pattern instead — one destination, a
 * command bar for the actions, and pivots for the views.
 *
 * The view lives in `?view=` rather than component state so a pivot is still linkable,
 * bookmarkable and back-button-able; the old routes redirect here rather than 404.
 */

export type LabView = "orders" | "overdue" | "payables" | "reports" | "overview";

export const LAB_VIEWS: ReadonlyArray<{
  id: LabView;
  label: string;
  icon: React.ElementType;
  /** Undefined means every role that can reach the workspace may see this pivot. */
  permission?: string;
}> = [
  { id: "orders",   label: "الطلبات",        icon: ClipboardList,    permission: PERMISSION_KEYS.LAB_ORDERS_VIEW },
  { id: "overdue",  label: "المتأخرة",       icon: Clock,            permission: PERMISSION_KEYS.LAB_ORDERS_VIEW },
  { id: "payables", label: "المستحقات",      icon: DollarSign,       permission: PERMISSION_KEYS.LAB_PAYABLES_VIEW },
  { id: "reports",  label: "التقارير",       icon: BarChart2,        permission: PERMISSION_KEYS.LAB_REPORTS_VIEW },
  { id: "overview", label: "نظرة عامة",      icon: LayoutDashboard,  permission: PERMISSION_KEYS.LAB_REPORTS_VIEW },
];

export function isLabView(value: string | null): value is LabView {
  return LAB_VIEWS.some((v) => v.id === value);
}

/**
 * Reads and writes the active pivot. Kept here so the shell and the redirect stubs agree on
 * the query-string contract.
 */
export function useLabView(): [LabView, (view: LabView) => void] {
  const router = useRouter();
  const params = useSearchParams();
  const raw = params.get("view");
  const active: LabView = isLabView(raw) ? raw : "orders";

  const setView = React.useCallback(
    (view: LabView) => {
      const next = new URLSearchParams(params.toString());
      if (view === "orders") next.delete("view");
      else next.set("view", view);
      const qs = next.toString();
      router.replace(qs ? `/lab?${qs}` : "/lab", { scroll: false });
    },
    [params, router],
  );

  return [active, setView];
}

export function LabWorkspaceShell({
  active,
  onChange,
  actions,
  children,
}: {
  active: LabView;
  onChange: (view: LabView) => void;
  /** Command-bar buttons contributed by the active pivot. */
  actions?: React.ReactNode;
  children: React.ReactNode;
}) {
  const canOrders   = useHasPermission(PERMISSION_KEYS.LAB_ORDERS_VIEW);
  const canPayables = useHasPermission(PERMISSION_KEYS.LAB_PAYABLES_VIEW);
  const canReports  = useHasPermission(PERMISSION_KEYS.LAB_REPORTS_VIEW);

  const allowed = React.useMemo(
    () => LAB_VIEWS.filter((v) => {
      if (v.permission === PERMISSION_KEYS.LAB_ORDERS_VIEW) return canOrders;
      if (v.permission === PERMISSION_KEYS.LAB_PAYABLES_VIEW) return canPayables;
      if (v.permission === PERMISSION_KEYS.LAB_REPORTS_VIEW) return canReports;
      return true;
    }),
    [canOrders, canPayables, canReports],
  );

  return (
    <div className="flex flex-col min-h-0">
      {/* ── Command bar ────────────────────────────────────────────────────── */}
      <div className="sticky top-0 z-20 bg-white border-b border-gray-200">
        <div className="flex items-center justify-between gap-3 px-4 pt-3 pb-2 flex-wrap">
          <div className="flex items-center gap-2 min-w-0">
            <span className="grid place-items-center w-8 h-8 rounded-md bg-cyan-50 shrink-0">
              <FlaskConical className="w-4 h-4 text-cyan-700" />
            </span>
            <div className="min-w-0">
              <h1 className="text-base font-semibold text-gray-900 truncate">المعامل والتراكيب</h1>
              <p className="text-xs text-gray-500 truncate">الطلبات والمتأخرات والمستحقات والتقارير في مكان واحد</p>
            </div>
          </div>

          {/* The active pivot owns its own actions — the bar is a slot, not a fixed menu,
              so a pivot can never show a button that does nothing on it. */}
          {actions && <div className="flex items-center gap-2 flex-wrap">{actions}</div>}
        </div>

        {/* ── Pivots ───────────────────────────────────────────────────────── */}
        <div
          role="tablist"
          aria-label="أقسام وحدة المعامل"
          className="flex items-stretch gap-1 px-3 overflow-x-auto"
        >
          {allowed.map((v) => {
            const Icon = v.icon;
            const isActive = v.id === active;
            return (
              <button
                key={v.id}
                role="tab"
                type="button"
                aria-selected={isActive}
                onClick={() => onChange(v.id)}
                className={cn(
                  "relative flex items-center gap-1.5 px-3 py-2 text-sm whitespace-nowrap transition-colors",
                  "border-b-2 -mb-px",
                  isActive
                    ? "border-cyan-600 text-cyan-700 font-semibold"
                    : "border-transparent text-gray-600 hover:text-gray-900 hover:bg-gray-50",
                )}
              >
                <Icon className="w-4 h-4" />
                <span>{v.label}</span>
              </button>
            );
          })}
        </div>
      </div>

      {/* Each pivot keeps its own scroll region so switching back does not lose position. */}
      <div className="flex-1 min-h-0">{children}</div>
    </div>
  );
}

/** Command-bar button, styled as one family so the bar reads as a single control strip. */
export function LabCommand({
  icon: Icon,
  label,
  onClick,
  primary,
  disabled,
  busy,
}: {
  icon: React.ElementType;
  label: string;
  onClick: () => void;
  primary?: boolean;
  disabled?: boolean;
  busy?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      disabled={disabled || busy}
      className={cn(
        "inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-sm transition-colors",
        "disabled:opacity-40 disabled:cursor-not-allowed",
        primary
          ? "bg-cyan-600 text-white hover:bg-cyan-700"
          : "border border-gray-200 text-gray-700 hover:bg-gray-50",
      )}
    >
      <Icon className={cn("w-4 h-4", busy && "animate-spin")} />
      <span>{label}</span>
    </button>
  );
}
