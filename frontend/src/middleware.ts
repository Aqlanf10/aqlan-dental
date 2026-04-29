import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

/**
 * Auth Middleware — Protects dashboard routes from unauthenticated access.
 *
 * Strategy:
 * - Check for access_token in sessionStorage (client-side) won't work in middleware
 *   because middleware runs on the edge (no DOM access).
 * - Instead, we check for a "aqlan-auth" cookie that we set alongside the token.
 * - The actual auth validation still happens server-side via JWT verification,
 *   but this middleware prevents the flash of dashboard content before redirect.
 */

const PUBLIC_PATHS = ["/login", "/api/auth"];

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

  // Check for auth cookie
  const authCookie = request.cookies.get("aqlan_auth_status");

  if (!authCookie || authCookie.value !== "authenticated") {
    // Redirect to login
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("redirect", pathname);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    /*
     * Match all request paths except:
     * - _next/static (static files)
     * - _next/image (image optimization)
     * - fonts (local fonts)
     * - favicon.ico
     */
    "/((?!_next/static|_next/image|fonts|favicon.ico).*)",
  ],
};
