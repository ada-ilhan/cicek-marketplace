import { describe, expect, it } from "vitest";
import { ApiError, kindForStatus, networkError, toApiError } from "./errors";

describe("toApiError", () => {
  it("maps problem details including field errors and trace id", () => {
    const error = toApiError(400, {
      title: "Invalid request",
      status: 400,
      errors: { email: ["'Email' is not a valid email address."] },
      traceId: "abc",
    });

    expect(error).toBeInstanceOf(ApiError);
    expect(error.kind).toBe("validation");
    expect(error.fieldErrors.email).toEqual(["'Email' is not a valid email address."]);
    expect(error.traceId).toBe("abc");
  });

  it("prefers detail over title for the message and keeps the code", () => {
    const error = toApiError(409, { title: "Conflict", detail: "Already exists.", code: "auth.email_taken" });

    expect(error.message).toBe("Already exists.");
    expect(error.code).toBe("auth.email_taken");
    expect(error.kind).toBe("conflict");
  });

  it("handles non-JSON bodies", () => {
    const error = toApiError(502, "<html>Bad gateway</html>");

    expect(error.kind).toBe("unavailable");
    expect(error.message).toBe("Request failed with status 502");
  });

  it.each([
    [401, "unauthorized"],
    [403, "forbidden"],
    [404, "not_found"],
    [422, "validation"],
    [429, "rate_limited"],
    [500, "server"],
  ] as const)("status %i maps to %s", (status, kind) => {
    expect(kindForStatus(status)).toBe(kind);
  });

  it("wraps network failures", () => {
    const cause = new TypeError("fetch failed");
    const error = networkError(cause);

    expect(error.kind).toBe("network");
    expect(error.cause).toBe(cause);
  });
});
