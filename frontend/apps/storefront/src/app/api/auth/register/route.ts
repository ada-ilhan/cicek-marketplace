import type { NextRequest } from "next/server";
import { registerSchema } from "@/features/auth/schemas";
import { handleCredentialsRequest } from "@/lib/auth-handlers";
import { apiClient, upstreamHeaders } from "@/lib/upstream";

export async function POST(request: NextRequest) {
  return handleCredentialsRequest(request, registerSchema, (body, correlationId) =>
    apiClient().POST("/api/v1/auth/register", { body, headers: upstreamHeaders(request, correlationId) }),
  );
}
