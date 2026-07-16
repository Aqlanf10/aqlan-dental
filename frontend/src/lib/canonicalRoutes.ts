export const CANONICAL_ROUTES = {
  dailyOperations: "/daily-operations",
  patientRecord: "/patients",
  permissionsSettings: "/settings?tab=permissions",
  orthodontics: "/ortho",
  hrAttendance: "/hr/attendance",
} as const;

export interface StaticRouteAlias {
  source: string;
  destination: string;
  capability: string;
}

export const STATIC_ROUTE_ALIASES: readonly StaticRouteAlias[] = [
  {
    source: "/clinic-queue",
    destination: `${CANONICAL_ROUTES.dailyOperations}?tab=queue`,
    capability: "Queue workspace",
  },
  {
    source: "/patient-journey",
    destination: CANONICAL_ROUTES.dailyOperations,
    capability: "Daily patient journey",
  },
  {
    source: "/users",
    destination: CANONICAL_ROUTES.permissionsSettings,
    capability: "Users and permissions",
  },
  {
    source: "/settings/users",
    destination: CANONICAL_ROUTES.permissionsSettings,
    capability: "Users and permissions",
  },
  {
    source: "/settings/permissions",
    destination: CANONICAL_ROUTES.permissionsSettings,
    capability: "Users and permissions",
  },
  {
    source: "/ortho-surgical",
    destination: CANONICAL_ROUTES.orthodontics,
    capability: "Orthodontic workspace",
  },
  {
    source: "/hr",
    destination: CANONICAL_ROUTES.hrAttendance,
    capability: "Human resources",
  },
] as const;

export function getStaticRouteAlias(source: string): StaticRouteAlias {
  const alias = STATIC_ROUTE_ALIASES.find((candidate) => candidate.source === source);
  if (!alias) {
    throw new Error(`Unknown legacy route alias: ${source}`);
  }

  return alias;
}

type SearchParamValue = string | string[] | undefined;

export function buildPatientJourneyDestination(
  patientId: string,
  searchParams: Record<string, SearchParamValue> = {},
): string {
  const query = new URLSearchParams();
  query.set("focus", "journey");

  for (const [key, value] of Object.entries(searchParams)) {
    if (key === "focus") continue;

    if (Array.isArray(value)) {
      for (const item of value) query.append(key, item);
    } else if (typeof value === "string") {
      query.append(key, value);
    }
  }

  return `${CANONICAL_ROUTES.patientRecord}/${encodeURIComponent(patientId)}?${query.toString()}`;
}
