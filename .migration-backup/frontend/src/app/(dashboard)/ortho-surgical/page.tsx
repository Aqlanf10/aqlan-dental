import { redirect } from "next/navigation";
import { getStaticRouteAlias } from "@/lib/canonicalRoutes";

export default function OrthoSurgicalLegacyListRoute() {
  redirect(getStaticRouteAlias("/ortho-surgical").destination);
}
