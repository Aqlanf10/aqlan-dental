import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

/**
 * Auth Middleware — Protects dashboard and portal routes from unauthenticated access.
 *
 * Strategy:
 * - Dashboard routes use the "aqlan_auth_status" cookie (admin auth)
 * - Portal routes use the "aqlan_portal_auth" cookie (patient auth)
 * - The actual auth validation still happens server-side via JWT verification,
 *   but this middleware prevents the flash of content before redirect.
 */

const PUBLIC_PATHS = ["/login", "/api/auth", "/api/public", "/api/portal", "/portal/login", "/queue-display", "/home"];

export function middleware(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow public paths
  if (PUBLIC_PATHS.some((p) => pathname.startsWith(p))) {
    return NextResponse.next();
  }

  // Allow static files and Next.js internals
  if (
    pathname.startsWith("/_next") ||
    pathname.startsWith("/fonts") ||
    pathname.includes(".") // static files like favicon.ico
  ) {
    return NextResponse.next();
  }

  // Portal routes use portal auth cookie
  if (pathname.startsWith("/portal")) {
    const portalCookie = request.cookies.get("aqlan_portal_auth");
    if (!portalCookie || portalCookie.value !== "authenticated") {
      const loginUrl = new URL("/portal/login", request.url);
      return NextResponse.redirect(loginUrl);
    }
    return NextResponse.next();
  }

  // Dashboard routes use admin auth cookie
  const authCookie = request.cookies.get("aqlan_auth_status");
  if (!authCookie || authCookie.value !== "authenticated") {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("redirect", pathname);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|fonts|favicon.ico).*)",
  ],
};
