import { ApiError } from "@cicek/api-client";

/**
 * Retry policy for queries: only transient failures (service unavailable, network) are retried, at most twice.
 * Definitive answers (401, 403, 404, validation, conflict) are shown immediately instead of after seconds of retries.
 */
export function retryTransientOnly(failureCount: number, error: unknown): boolean {
  return (
    failureCount < 2 &&
    error instanceof ApiError &&
    (error.kind === "unavailable" || error.kind === "network")
  );
}
