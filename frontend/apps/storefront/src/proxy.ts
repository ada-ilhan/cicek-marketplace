import { NextResponse, type NextRequest } from "next/server";

/**
 * Route guard for private pages (UX only — the API authorizes every request). Redirects to the login
 * page when no session cookie is present. Next.js 16 "proxy" (formerly middleware).
 */
export function proxy(request: NextRequest) {
  const hasSession = request.cookies.has("__Host-cicek_rt") || request.cookies.has("cicek_rt");
  if (hasSession) return NextResponse.next();

  const login = new URL("/login", request.url);
  login.searchParams.set("next", request.nextUrl.pathname);
  return NextResponse.redirect(login);
}

export const config = {
  matcher: ["/account/:path*", "/cart", "/checkout", "/seller/:path*", "/admin/:path*"],
};
