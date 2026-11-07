/**
 * Pure request-security helpers used by the BFF route handlers (unit tested; no Next.js imports).
 */

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

/**
 * CSRF defence for cookie-authenticated BFF routes: state-changing requests must originate from this
 * app. Combined with SameSite=Lax cookies this blocks cross-site form posts and fetches.
 */
export function isSameOriginRequest(method: string, headers: Headers, appOrigin: string): boolean {
  if (SAFE_METHODS.has(method.toUpperCase())) return true;
  const origin = headers.get("origin");
  if (origin) return origin === appOrigin;
  return headers.get("sec-fetch-site") === "same-origin";
}

const SEGMENT = /^[A-Za-z0-9._~-]+$/;

/**
 * Validates the catch-all path forwarded by the BFF proxy. Only versioned API resources are reachable;
 * traversal segments and the token endpoints (handled by dedicated routes) are rejected.
 */
export function toUpstreamApiPath(segments: readonly string[]): string | null {
  if (segments.length < 2 || segments[0] !== "v1") return null;
  if (segments.some((s) => s === "." || s === ".." || !SEGMENT.test(s))) return null;
  if (segments[1] === "auth") return null;
  return `/api/${segments.join("/")}`;
}

/** Accepts only same-site relative paths for post-login redirects (prevents open redirects). */
export function safeRedirectPath(candidate: string | null | undefined, fallback = "/account"): string {
  if (!candidate || !candidate.startsWith("/") || candidate.startsWith("//") || candidate.startsWith("/\\")) {
    return fallback;
  }
  return candidate;
}
