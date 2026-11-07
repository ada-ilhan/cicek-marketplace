import { isSameOriginRequest, safeRedirectPath, toUpstreamApiPath } from "./security";

const origin = "https://shop.example.com";

describe("isSameOriginRequest", () => {
  it("allows safe methods regardless of origin", () => {
    expect(isSameOriginRequest("GET", new Headers({ origin: "https://evil.example" }), origin)).toBe(true);
  });

  it("allows mutations from the app origin", () => {
    expect(isSameOriginRequest("POST", new Headers({ origin }), origin)).toBe(true);
  });

  it("rejects cross-site mutations", () => {
    expect(isSameOriginRequest("POST", new Headers({ origin: "https://evil.example" }), origin)).toBe(false);
    expect(isSameOriginRequest("DELETE", new Headers({ "sec-fetch-site": "cross-site" }), origin)).toBe(
      false,
    );
  });

  it("falls back to Sec-Fetch-Site when Origin is absent", () => {
    expect(isSameOriginRequest("POST", new Headers({ "sec-fetch-site": "same-origin" }), origin)).toBe(true);
    expect(isSameOriginRequest("POST", new Headers(), origin)).toBe(false);
  });
});

describe("toUpstreamApiPath", () => {
  it("maps versioned resources", () => {
    expect(toUpstreamApiPath(["v1", "me"])).toBe("/api/v1/me");
    expect(toUpstreamApiPath(["v1", "admin", "system", "status"])).toBe("/api/v1/admin/system/status");
  });

  it.each([
    [["v1", "..", "health"]],
    [["v1", "."]],
    [["v2", "me"]],
    [["v1"]],
    [["v1", "auth", "login"]],
    [["v1", "me%2F..%2Fadmin"]],
    [["v1", "http:", "evil.example"]],
  ])("rejects %j", (segments) => {
    expect(toUpstreamApiPath(segments)).toBeNull();
  });
});

describe("safeRedirectPath", () => {
  it.each([
    ["/account", "/account"],
    ["/account?tab=orders", "/account?tab=orders"],
    ["//evil.example", "/account"],
    ["/\\evil.example", "/account"],
    ["https://evil.example", "/account"],
    [null, "/account"],
  ])("%s → %s", (input, expected) => {
    expect(safeRedirectPath(input)).toBe(expected);
  });
});
