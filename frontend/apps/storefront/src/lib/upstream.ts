import "server-only";
import { createApiClient, type AuthSession } from "@cicek/api-client";
import { NextResponse } from "next/server";
import { serverConfig } from "./server-config";

/** Headers forwarded to the API: client IP (for rate limiting) and a correlation id for tracing. */
export function upstreamHeaders(request: Request, correlationId: string): Record<string, string> {
  const headers: Record<string, string> = { "X-Correlation-Id": correlationId };
  const forwardedFor = request.headers.get("x-forwarded-for")?.split(",")[0]?.trim();
  if (forwardedFor) headers["X-Forwarded-For"] = forwardedFor;
  return headers;
}

export function newCorrelationId(): string {
  return crypto.randomUUID().replaceAll("-", "");
}

export function apiClient() {
  return createApiClient({ baseUrl: serverConfig().apiBaseUrl });
}

/** Problem Details response produced by the BFF itself (API problems are passed through unchanged). */
export function bffProblem(
  status: number,
  title: string,
  code: string,
  extra?: Record<string, unknown>,
): NextResponse {
  return NextResponse.json(
    { title, status, code, ...extra },
    { status, headers: { "Content-Type": "application/problem+json", "Cache-Control": "no-store" } },
  );
}

export function unavailableProblem(): NextResponse {
  return bffProblem(503, "The service is temporarily unavailable.", "service.unavailable");
}

/** Exchanges a refresh token for a new session. Returns null when the API rejects it. Throws on network failure. */
export async function refreshSession(
  refreshToken: string,
  request: Request,
  correlationId: string,
): Promise<AuthSession | null> {
  const { data, response } = await apiClient().POST("/api/v1/auth/refresh", {
    body: { refreshToken },
    headers: upstreamHeaders(request, correlationId),
  });
  return response.ok && data ? data : null;
}
