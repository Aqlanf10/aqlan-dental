import { redirect } from "next/navigation";

/**
 * CORE-LAB-017 — the lab module is one workspace now. This route is kept as a permanent
 * redirect so links, bookmarks and the browser history from before the merge still work.
 */
export default function LabRouteRedirect() {
  redirect("/lab?view=overview");
}
