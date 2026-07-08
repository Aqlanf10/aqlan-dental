import { redirect } from "next/navigation";

// SEQ-03: permissions management lives in the unified settings hub
// (permissions tab = UsersTab + RolesTab). This legacy path used to 404
// (QA4-03) — redirect instead, same pattern as hr/page.tsx (QA3-05).
export default function SettingsPermissionsRoute() {
  redirect("/settings?tab=permissions");
}
