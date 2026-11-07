import type { AuthSession } from "@cicek/api-client";
import { NextResponse, type NextRequest } from "next/server";
import { isSameOriginRequest, toUpstreamApiPath } from "@/lib/security";
import { serverConfig } from "@/lib/server-config";
import { clearSessionCookies, readSessionTokens, setSessionCookies } from "@/lib/session";
import {
  bffProblem,
  newCorrelationId,
  refreshSession,
  unavailableProblem,
  upstreamHeaders,
} from "@/lib/upstream";

type RouteContext = { params: Promise<{ path: string[] }> };

const FORWARDED_RESPONSE_HEADERS = ["content-type", "retry-after", "x-correlation-id"];

/**
 * Backend-for-frontend proxy: the browser calls /api/bff/v1/..., this handler attaches the access token
 * from the HttpOnly cookie and forwards to the API. Expired access tokens are refreshed transparently
 * (once per request); a rejected refresh clears the session cookies.
 */
async function proxy(request: NextRequest, context: RouteContext): Promise<NextResponse> {
  const config = serverConfig();
  const upstreamPath = toUpstreamApiPath((await context.params).path);
  if (!upstreamPath) {
    return bffProblem(404, "Not found", "bff.unknown_path");
  }

  if (!isSameOriginRequest(request.method, request.headers, config.appOrigin)) {
    return bffProblem(403, "Cross-site request rejected.", "csrf.rejected");
  }

  const correlationId = newCorrelationId();
  const tokens = readSessionTokens(request.cookies, config.cookieSecure);
  const body = ["GET", "HEAD"].includes(request.method) ? undefined : await request.arrayBuffer();
  let refreshed: AuthSession | null | undefined;

  try {
    if (!tokens.accessToken && tokens.refreshToken) {
      refreshed = await refreshSession(tokens.refreshToken, request, correlationId);
    }

    let upstream = await forward(
      request,
      upstreamPath,
      body,
      refreshed?.accessToken ?? tokens.accessToken,
      correlationId,
    );

    // 401 means the request was not processed, so retrying once with a fresh token is safe for any method.
    if (upstream.status === 401 && tokens.refreshToken && refreshed === undefined) {
      refreshed = await refreshSession(tokens.refreshToken, request, correlationId);
      if (refreshed) {
        upstream = await forward(request, upstreamPath, body, refreshed.accessToken, correlationId);
      }
    }

    const response = new NextResponse(upstream.status === 204 ? null : upstream.body, {
      status: upstream.status,
    });
    for (const name of FORWARDED_RESPONSE_HEADERS) {
      const value = upstream.headers.get(name);
      if (value) response.headers.set(name, value);
    }
    response.headers.set("Cache-Control", "no-store");

    if (refreshed) {
      setSessionCookies(response, refreshed, config.cookieSecure);
    } else if (refreshed === null) {
      clearSessionCookies(response, config.cookieSecure);
    }

    return response;
  } catch {
    return unavailableProblem();
  }
}

function forward(
  request: NextRequest,
  path: string,
  body: ArrayBuffer | undefined,
  accessToken: string | undefined,
  correlationId: string,
): Promise<Response> {
  const url = new URL(path, serverConfig().apiBaseUrl);
  url.search = request.nextUrl.search;
  const headers: Record<string, string> = {
    Accept: "application/json",
    ...upstreamHeaders(request, correlationId),
  };
  const contentType = request.headers.get("content-type");
  if (contentType) headers["Content-Type"] = contentType;
  const idempotencyKey = request.headers.get("idempotency-key");
  if (idempotencyKey) headers["Idempotency-Key"] = idempotencyKey;
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`;

  return fetch(url, { method: request.method, headers, body, cache: "no-store", redirect: "manual" });
}

export { proxy as GET, proxy as POST, proxy as PUT, proxy as PATCH, proxy as DELETE };
