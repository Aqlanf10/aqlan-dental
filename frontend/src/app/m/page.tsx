import { redirect } from "next/navigation";

/** The app opens on the schedule — the reason to pick up the phone in the first place. */
export default function MobileHome() {
  redirect("/m/appointments");
}
