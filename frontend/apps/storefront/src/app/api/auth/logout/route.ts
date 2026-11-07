import { NextResponse, type NextRequest } from "next/server";
import { isSameOriginRequest } from "@/lib/security";
import { serverConfig } from "@/lib/server-config";
import { clearSessionCookies, readSessionTokens } from "@/lib/session";
import { apiClient, bffProblem, newCorrelationId, upstreamHeaders } from "@/lib/upstream";

/** Revokes the session family at the API (best effort) and always clears the cookies. */
export async function POST(request: NextRequest) {
  const config = serverConfig();
  if (!isSameOriginRequest(request.method, request.headers, config.appOrigin)) {
    return bffProblem(403, "Cross-site request rejected.", "csrf.rejected");
  }

  const { refreshToken } = readSessionTokens(request.cookies, config.cookieSecure);
  if (refreshToken) {
    try {
      await apiClient().POST("/api/v1/auth/logout", {
        body: { refreshToken },
        headers: upstreamHeaders(request, newCorrelationId()),
      });
    } catch {
      // The API being unreachable must not keep the user signed in on this device.
    }
  }

  const response = new NextResponse(null, { status: 204 });
  clearSessionCookies(response, config.cookieSecure);
  return response;
}
