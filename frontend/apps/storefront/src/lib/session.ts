import "server-only";
import type { AuthSession } from "@cicek/api-client";
import type { NextResponse } from "next/server";

/** Cookie names; the __Host- prefix (secure only) pins cookies to this exact origin and path "/". */
export function cookieNames(secure: boolean) {
  return {
    access: secure ? "__Host-cicek_at" : "cicek_at",
    refresh: secure ? "__Host-cicek_rt" : "cicek_rt",
  } as const;
}

interface CookieReader {
  get(name: string): { value: string } | undefined;
}

export interface SessionTokens {
  accessToken: string | undefined;
  refreshToken: string | undefined;
}

export function readSessionTokens(cookies: CookieReader, secure: boolean): SessionTokens {
  const names = cookieNames(secure);
  return { accessToken: cookies.get(names.access)?.value, refreshToken: cookies.get(names.refresh)?.value };
}

/**
 * Stores the API tokens in HttpOnly cookies so client-side JavaScript can never read them.
 * The access cookie expires with the token, which makes the BFF refresh proactively.
 */
export function setSessionCookies(response: NextResponse, session: AuthSession, secure: boolean): void {
  const names = cookieNames(secure);
  const base = { httpOnly: true, secure, sameSite: "lax" as const, path: "/" };
  response.cookies.set(names.access, session.accessToken, {
    ...base,
    expires: new Date(session.accessTokenExpiresAt),
  });
  response.cookies.set(names.refresh, session.refreshToken, {
    ...base,
    expires: new Date(session.refreshTokenExpiresAt),
  });
}

export function clearSessionCookies(response: NextResponse, secure: boolean): void {
  const names = cookieNames(secure);
  const base = { httpOnly: true, secure, sameSite: "lax" as const, path: "/", maxAge: 0 };
  response.cookies.set(names.access, "", base);
  response.cookies.set(names.refresh, "", base);
}
