import { redirect } from "@/lib/nextNavCompat";
import { getStaticRouteAlias } from "@/lib/canonicalRoutes";

// SEQ-03: there is no standalone users screen — user management lives in the
// unified settings hub (permissions tab = UsersTab + RolesTab). This path
// used to 404 (QA4-03) — redirect instead, same pattern as hr/page.tsx.
export default function UsersRoute() {
  redirect(getStaticRouteAlias("/users").destination);
}
