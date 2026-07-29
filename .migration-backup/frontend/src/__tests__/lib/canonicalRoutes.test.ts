import { describe, expect, it } from "vitest";
import {
  buildPatientJourneyDestination,
  CANONICAL_ROUTES,
  getStaticRouteAlias,
  STATIC_ROUTE_ALIASES,
} from "@/lib/canonicalRoutes";
import { isRouteAllowed } from "@/lib/routePermissions";

const STAFF_ROLES = [
  "Admin",
  "Reception",
  "GeneralDentist",
  "OralSurgeon",
  "Orthodontist",
  "Accountant",
  "Assistant",
  "BranchManager",
] as const;

describe("canonical route ownership", () => {
  it("keeps every legacy route source unique", () => {
    const sources = STATIC_ROUTE_ALIASES.map((alias) => alias.source);
    expect(new Set(sources).size).toBe(sources.length);
  });

  it.each([
    ["/clinic-queue", "/daily-operations?tab=queue"],
    ["/patient-journey", "/daily-operations"],
    ["/users", "/settings?tab=permissions"],
    ["/settings/users", "/settings?tab=permissions"],
    ["/settings/permissions", "/settings?tab=permissions"],
    ["/ortho-surgical", "/ortho"],
    ["/hr", "/hr/attendance"],
  ])("maps %s to its canonical owner", (source, destination) => {
    expect(getStaticRouteAlias(source).destination).toBe(destination);
  });

  it("fails closed when a redirect page references an unregistered alias", () => {
    expect(() => getStaticRouteAlias("/unknown-legacy-route")).toThrow(
      "Unknown legacy route alias: /unknown-legacy-route",
    );
  });

  it.each(STATIC_ROUTE_ALIASES)(
    "keeps $source access no broader than its canonical owner",
    ({ source, destination }) => {
      const ownerPath = destination.split("?", 1)[0];
      const aliasRoles = STAFF_ROLES.filter((role) => isRouteAllowed(source, role));
      const ownerRoles = STAFF_ROLES.filter((role) => isRouteAllowed(ownerPath, role));
      expect(aliasRoles).toEqual(ownerRoles);
    },
  );

  it("keeps the patient journey detail alias aligned with patient records", () => {
    for (const role of STAFF_ROLES) {
      expect(isRouteAllowed("/patient-journey/patient-1", role)).toBe(
        isRouteAllowed("/patients/patient-1", role),
      );
    }
  });

  it("moves patient journey details into the canonical patient record", () => {
    expect(
      buildPatientJourneyDestination("patient/42", {
        tab: "timeline",
        filter: ["today", "open"],
        empty: undefined,
      }),
    ).toBe(
      `${CANONICAL_ROUTES.patientRecord}/patient%2F42?focus=journey&tab=timeline&filter=today&filter=open`,
    );
  });

  it("does not let a legacy focus parameter override the canonical journey focus", () => {
    expect(buildPatientJourneyDestination("patient-1", { focus: "finance" })).toBe(
      "/patients/patient-1?focus=journey",
    );
  });
});
