import "server-only";
import { createApiClient } from "@cicek/api-client";
import { serverConfig } from "./server-config";

export type Loaded<T> = { ok: true; data: T } | { ok: false; status: number; traceId?: string | undefined };

/** API client for anonymous, public catalogue reads from server components (no session involved). */
export function publicApi() {
  return createApiClient({ baseUrl: serverConfig().apiBaseUrl });
}

/** Runs a public API call and converts failures (including network errors) into a renderable result. */
export async function load<T>(
  call: () => Promise<{ data?: T; error?: unknown; response: Response }>,
): Promise<Loaded<T>> {
  try {
    const { data, error, response } = await call();
    if (response.ok && data !== undefined) return { ok: true, data };
    const traceId =
      typeof error === "object" && error !== null && "traceId" in error ? String(error.traceId) : undefined;
    return { ok: false, status: response.status, traceId };
  } catch {
    return { ok: false, status: 503 };
  }
}
