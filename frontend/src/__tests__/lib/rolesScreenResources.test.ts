import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { execFileSync } from "node:child_process";

/**
 * The roles screen must offer the owner the resources the server actually reads.
 *
 * Found in the go-live dry run as Reception. `RolePermissions` is seeded with two
 * invoice resources: `invoices` (Reception create = true) and `finance.invoices`
 * (Reception create = false). Every API guard reads `finance.invoices`; nothing
 * anywhere reads `invoices`. The settings screen listed the one nothing reads and
 * omitted the one every guard reads, so the owner saw «الفواتير / إنشاء» switched
 * ON for Reception while the API answered 403, and had no switch for the resource
 * that would have changed the answer.
 *
 * Two rules, both one-directional:
 *   1. Every resource the screen offers must be one some API guard reads.
 *   2. Every resource an API guard reads must be offered somewhere in the screen —
 *      otherwise it is enforced but unreachable, which is how `finance.*` sat
 *      invisible while governing all of finance.
 *
 * Resources enforced only by role policy (`[Authorize(Policy = ...)]`) rather than
 * by `RolePermissions` are listed in UI_ONLY below: their switches drive button
 * visibility in the frontend, not the server's answer. That distinction is real and
 * is tracked separately — this test only guards the permission-guard family.
 */

const repoRoot = resolve(__dirname, "../../../..");

/** Resources the screen may list even though no PermissionGuard reads them:
 *  they gate frontend affordances, and the server enforces those routes by role policy. */
const UI_ONLY = new Set([
  "daily_operations", "booking_requests", "clinic_queue", "clinic_display",
  "patient_journey", "visits", "checkout", "rooms",
  "patients", "appointments", "reports",
  "ortho", "general_dentistry", "surgery",
  "users", "user_management", "settings", "ai",
  "password_reset_requests", "impersonation",
]);

function screenResources(): string[] {
  const src = readFileSync(
    resolve(repoRoot, "frontend/src/app/(dashboard)/settings/_components/RolesTab.tsx"),
    "utf8",
  );
  const start = src.indexOf("const PERMISSION_GROUPS");
  expect(start, "PERMISSION_GROUPS must exist in RolesTab").toBeGreaterThan(-1);
  const end = src.indexOf("];", start);
  const block = src.slice(start, end);
  return [...new Set(block.match(/"[a-z_.]+"/g)?.map((s) => s.slice(1, -1)) ?? [])];
}

/** Resources read by PermissionGuard.HasAsync / CanAsync anywhere in the API. */
function guardedResources(): string[] {
  const out = execFileSync(
    "grep",
    ["-rhoE", "(PermissionGuard\\.HasAsync|CanAsync)\\([^)]*\\)", resolve(repoRoot, "backend/src")],
    { encoding: "utf8" },
  );
  const actions = new Set(["view", "create", "edit", "delete", "export", "approve"]);
  const found = new Set<string>();
  for (const m of out.match(/"[a-z_.]+"/g) ?? []) {
    const r = m.slice(1, -1);
    if (!actions.has(r)) found.add(r);
  }
  return [...found];
}

describe("settings → roles screen offers the resources the server reads", () => {
  it("lists no resource that neither a guard nor the frontend consults", () => {
    const guarded = new Set(guardedResources());
    const orphans = screenResources().filter((r) => !guarded.has(r) && !UI_ONLY.has(r));
    expect(orphans, "switches here change nothing — remove them or wire them up").toEqual([]);
  });

  it("offers every resource an API guard reads", () => {
    const shown = new Set(screenResources());
    // ortho_surgical is granted by role policy on a shared workspace, not per-role rows.
    const missing = guardedResources().filter((r) => !shown.has(r) && r !== "ortho_surgical");
    expect(missing, "enforced server-side but the owner cannot see or change it").toEqual([]);
  });

  it("does not offer the phantom `invoices` resource", () => {
    expect(screenResources()).not.toContain("invoices");
    expect(screenResources()).toContain("finance.invoices");
  });
});
