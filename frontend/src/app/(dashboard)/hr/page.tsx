import { redirect } from "next/navigation";

// /hr has no index screen of its own — the sidebar links directly to the four
// sub-pages. Redirect instead of 404 for anyone landing on the bare path.
export default function HrIndexRoute() {
  redirect("/hr/attendance");
}
