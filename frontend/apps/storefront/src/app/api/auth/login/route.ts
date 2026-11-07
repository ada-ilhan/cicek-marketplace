import type { NextRequest } from "next/server";
import { loginSchema } from "@/features/auth/schemas";
import { handleCredentialsRequest } from "@/lib/auth-handlers";
import { apiClient, upstreamHeaders } from "@/lib/upstream";

export async function POST(request: NextRequest) {
  return handleCredentialsRequest(request, loginSchema, (body, correlationId) =>
    apiClient().POST("/api/v1/auth/login", { body, headers: upstreamHeaders(request, correlationId) }),
  );
}
