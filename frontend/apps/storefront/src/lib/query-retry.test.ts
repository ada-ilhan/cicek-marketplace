import { ApiError } from "@cicek/api-client";
import { retryTransientOnly } from "./query-retry";

const error = (kind: ApiError["kind"]) => new ApiError({ kind, status: 0, message: "x" });

describe("retryTransientOnly", () => {
  it.each(["unavailable", "network"] as const)("retries %s failures up to twice", (kind) => {
    expect(retryTransientOnly(0, error(kind))).toBe(true);
    expect(retryTransientOnly(1, error(kind))).toBe(true);
    expect(retryTransientOnly(2, error(kind))).toBe(false);
  });

  it.each(["unauthorized", "forbidden", "not_found", "validation", "conflict", "server"] as const)(
    "never retries %s",
    (kind) => {
      expect(retryTransientOnly(0, error(kind))).toBe(false);
    },
  );

  it("does not retry unknown errors", () => {
    expect(retryTransientOnly(0, new Error("boom"))).toBe(false);
  });
});
