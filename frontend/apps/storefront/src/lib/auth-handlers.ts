import "server-only";
import type { AuthSession } from "@cicek/api-client";
import { NextResponse, type NextRequest } from "next/server";
import type { z } from "zod";
import { isSameOriginRequest } from "./security";
import { serverConfig } from "./server-config";
import { setSessionCookies } from "./session";
import { bffProblem, newCorrelationId, unavailableProblem } from "./upstream";

type UpstreamResult = { data?: AuthSession; error?: unknown; response: Response };

/**
 * Shared flow for the login and register BFF routes: CSRF check → schema validation (same rules as the
 * API, which validates again) → API call → tokens into HttpOnly cookies. Only the user is returned to JS.
 */
export async function handleCredentialsRequest<T>(
  request: NextRequest,
  schema: z.ZodType<T>,
  callApi: (body: T, correlationId: string) => Promise<UpstreamResult>,
): Promise<NextResponse> {
  const config = serverConfig();
  if (!isSameOriginRequest(request.method, request.headers, config.appOrigin)) {
    return bffProblem(403, "Cross-site request rejected.", "csrf.rejected");
  }

  const parsed = schema.safeParse(await request.json().catch(() => null));
  if (!parsed.success) {
    const errors: Record<string, string[]> = {};
    for (const issue of parsed.error.issues) {
      const field = String(issue.path[0] ?? "request");
      (errors[field] ??= []).push(issue.message);
    }
    return bffProblem(400, "Invalid request", "request.invalid", { errors });
  }

  const correlationId = newCorrelationId();
  let result: UpstreamResult;
  try {
    result = await callApi(parsed.data, correlationId);
  } catch {
    return unavailableProblem();
  }

  if (!result.response.ok || !result.data) {
    // API problem details are already safe for clients; pass them through with the original status.
    return NextResponse.json(result.error ?? { title: "Request failed", status: result.response.status }, {
      status: result.response.status,
      headers: { "Content-Type": "application/problem+json", "Cache-Control": "no-store" },
    });
  }

  const response = NextResponse.json(
    { user: result.data.user },
    { headers: { "Cache-Control": "no-store" } },
  );
  setSessionCookies(response, result.data, config.cookieSecure);
  return response;
}
