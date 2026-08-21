import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { ROUTE_MANIFEST, getNavigationRoles } from "@/lib/routePermissions";

/**
 * Phase 1 exit gate — the frontend half of `contracts/route-policy-map.json`.
 *
 * The gate is "sidebar/guards/server policy agree". CORE-P1-S3 gave the sidebar and route
 * guards one manifest; CORE-P1-S4 pinned each controller's backend policy. Neither checked
 * that the two agree, and they are written in different languages, so nothing could — which
 * is why the shared JSON contract exists. The backend proves its role sets are what its own
 * container grants; this proves no route invites a role the server will refuse.
 *
 * The rule is deliberately one-directional. A frontend NARROWER than the server is fine: the
 * screen simply is not offered, and the server would have allowed it. A frontend BROADER than
 * the server is the bug — the sidebar shows an entry, the guard lets the role through, every
 * API call comes back 403, and the user lands on a dead end or is bounced back where they
 * started. That has happened at least twice in this codebase (Reception on `/finance-v3`, and
 * the surgeon bounce on `/ortho`), which is why it is a test and not a convention.
 */

interface ContractRoute {
  path: string;
  owner: string;
  policy: string;
  frontendMayBeBroader?: { extraRoles: string[]; reason: string };
}

interface Contract {
  policyRoles: Record<string, string[]>;
  routes: ContractRoute[];
}

const contract: Contract = JSON.parse(
  readFileSync(resolve(__dirname, "../../../../contracts/route-policy-map.json"), "utf8"),
);

/** Admin is allowed everywhere by `isRouteAllowed`, so it is never the interesting case. */
const withoutAdmin = (roles: readonly string[]) => roles.filter((r) => r !== "Admin");

describe("route guards agree with backend policy", () => {
  it("reads a contract with real content", () => {
    // A malformed or empty contract would make every test below pass by iterating nothing.
    expect(contract.routes.length).toBeGreaterThan(20);
    expect(Object.keys(contract.policyRoles).length).toBeGreaterThan(10);
  });

  it("never lets a route admit a role the server would refuse", () => {
    const violations: string[] = [];

    for (const route of contract.routes) {
      const entry = ROUTE_MANIFEST.find((r) => r.path === route.path);
      if (!entry) continue; // covered by its own test below

      const serverRoles = new Set(contract.policyRoles[route.policy] ?? []);
      const permitted = new Set(route.frontendMayBeBroader?.extraRoles ?? []);

      const tooBroad = withoutAdmin(entry.allowedRoles).filter(
        (role) => !serverRoles.has(role) && !permitted.has(role),
      );

      if (tooBroad.length > 0) {
        violations.push(
          `${route.path}: guard admits [${tooBroad.join(", ")}] but ${route.owner} enforces ` +
            `${route.policy} = [${[...serverRoles].join(", ")}]`,
        );
      }
    }

    expect(violations, violations.join("\n")).toEqual([]);
  });

  /**
   * The sidebar is what a user actually sees. Showing an entry the guard would then reject is
   * the same dead end one step earlier.
   */
  it("never advertises a route in the sidebar that the server would refuse", () => {
    const violations: string[] = [];

    for (const route of contract.routes) {
      const entry = ROUTE_MANIFEST.find((r) => r.path === route.path);
      if (!entry) continue;

      const serverRoles = new Set(contract.policyRoles[route.policy] ?? []);
      const permitted = new Set(route.frontendMayBeBroader?.extraRoles ?? []);

      const advertised = withoutAdmin(getNavigationRoles(route.path)).filter(
        (role) => !serverRoles.has(role) && !permitted.has(role),
      );

      if (advertised.length > 0) {
        violations.push(`${route.path}: sidebar offers [${advertised.join(", ")}] to no purpose`);
      }
    }

    expect(violations, violations.join("\n")).toEqual([]);
  });

  it("describes routes that still exist", () => {
    const known = new Set(ROUTE_MANIFEST.map((r) => r.path));
    const stale = contract.routes.map((r) => r.path).filter((p) => !known.has(p));

    expect(stale, `stale contract entries: ${stale.join(", ")}`).toEqual([]);
  });

  /**
   * A deliberate exception has to say why. An `extraRoles` list with no reason is how a
   * violation gets waved through and then read later as intentional.
   */
  it("requires a written reason for every role allowed past the server's policy", () => {
    const unexplained = contract.routes
      .filter((r) => r.frontendMayBeBroader)
      .filter((r) => (r.frontendMayBeBroader!.reason ?? "").trim().length < 40)
      .map((r) => r.path);

    expect(unexplained, `exceptions with no real reason: ${unexplained.join(", ")}`).toEqual([]);
  });

  /**
   * An exception that is no longer needed stops being documentation and becomes a hole:
   * it would silently permit a future widening of the same route.
   */
  it("keeps no exception that has stopped being necessary", () => {
    const unnecessary: string[] = [];

    for (const route of contract.routes) {
      if (!route.frontendMayBeBroader) continue;

      const entry = ROUTE_MANIFEST.find((r) => r.path === route.path);
      if (!entry) continue;

      const serverRoles = new Set(contract.policyRoles[route.policy] ?? []);
      const stillUsed = route.frontendMayBeBroader.extraRoles.filter(
        (role) => entry.allowedRoles.includes(role) && !serverRoles.has(role),
      );

      if (stillUsed.length === 0) {
        unnecessary.push(`${route.path}: exception no longer applies — remove it`);
      }
    }

    expect(unnecessary, unnecessary.join("\n")).toEqual([]);
  });
});
