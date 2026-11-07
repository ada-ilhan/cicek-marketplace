import { networkError, toApiError } from "@cicek/api-client";

/**
 * Browser-side fetch to this app's BFF routes (same origin; cookies sent automatically). Every failure
 * is thrown as an ApiError, so components handle one error type.
 */
export async function bffFetch<T>(
  path: string,
  init: { method?: string; body?: unknown; headers?: Record<string, string> } = {},
): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      method: init.method ?? "GET",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        ...(init.body === undefined ? {} : { "Content-Type": "application/json" }),
        ...init.headers,
      },
      body: init.body === undefined ? undefined : JSON.stringify(init.body),
    });
  } catch (cause) {
    throw networkError(cause);
  }

  if (response.status === 204) return undefined as T;
  const text = await response.text();
  let payload: unknown = text;
  try {
    payload = text ? JSON.parse(text) : undefined;
  } catch {
    // Non-JSON body (e.g. proxy error page): keep the text; toApiError copes with it.
  }

  if (!response.ok) throw toApiError(response.status, payload);
  return payload as T;
}

/** Uploads one file as multipart/form-data (the browser sets the boundary; no JSON content type). */
export async function bffUpload<T>(path: string, file: File): Promise<T> {
  const form = new FormData();
  form.append("file", file, file.name);
  let response: Response;
  try {
    response = await fetch(path, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" },
      body: form,
    });
  } catch (cause) {
    throw networkError(cause);
  }

  const text = await response.text();
  let payload: unknown = text;
  try {
    payload = text ? JSON.parse(text) : undefined;
  } catch {
    // Non-JSON body (e.g. a proxy's "413 Payload Too Large" page): toApiError copes with it.
  }

  if (!response.ok) throw toApiError(response.status, payload);
  return payload as T;
}
