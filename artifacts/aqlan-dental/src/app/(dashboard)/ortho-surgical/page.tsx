import { redirect } from "@/lib/nextNavCompat";
import { getStaticRouteAlias } from "@/lib/canonicalRoutes";

export default function OrthoSurgicalLegacyListRoute() {
  redirect(getStaticRouteAlias("/ortho-surgical").destination);
}
