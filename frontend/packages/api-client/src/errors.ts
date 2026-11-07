/**
 * Centralised API error model. Every failed call — API problem details, BFF errors, network failures —
 * is normalised into an {@link ApiError} so UI code handles exactly one shape.
 */

/** RFC 9457 problem details as emitted by the Çiçek API. */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  correlationId?: string;
  errors?: Record<string, string[]>;
}

export type ApiErrorKind =
  | "validation"
  | "unauthorized"
  | "forbidden"
  | "not_found"
  | "conflict"
  | "rate_limited"
  | "unavailable"
  | "network"
  | "server";

export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number;
  readonly code: string | undefined;
  readonly fieldErrors: Readonly<Record<string, readonly string[]>>;
  readonly traceId: string | undefined;

  constructor(init: {
    kind: ApiErrorKind;
    status: number;
    message: string;
    code?: string | undefined;
    fieldErrors?: Record<string, string[]>;
    traceId?: string | undefined;
  }) {
    super(init.message);
    this.name = "ApiError";
    this.kind = init.kind;
    this.status = init.status;
    this.code = init.code;
    this.fieldErrors = init.fieldErrors ?? {};
    this.traceId = init.traceId;
  }
}

export function kindForStatus(status: number): ApiErrorKind {
  if (status === 400 || status === 422) return "validation";
  if (status === 401) return "unauthorized";
  if (status === 403) return "forbidden";
  if (status === 404) return "not_found";
  if (status === 409) return "conflict";
  if (status === 429) return "rate_limited";
  if (status === 502 || status === 503 || status === 504) return "unavailable";
  return "server";
}

function isProblemDetails(value: unknown): value is ProblemDetails {
  return (
    typeof value === "object" && value !== null && ("title" in value || "status" in value || "code" in value)
  );
}

/** Builds an {@link ApiError} from an HTTP status and a (possibly non-JSON) response body. */
export function toApiError(status: number, body: unknown): ApiError {
  const problem = isProblemDetails(body) ? body : undefined;
  return new ApiError({
    kind: kindForStatus(status),
    status,
    message: problem?.detail ?? problem?.title ?? `Request failed with status ${status}`,
    code: problem?.code,
    fieldErrors: problem?.errors,
    traceId: problem?.traceId,
  });
}

export function networkError(cause: unknown): ApiError {
  const error = new ApiError({ kind: "network", status: 0, message: "Network request failed" });
  error.cause = cause;
  return error;
}
