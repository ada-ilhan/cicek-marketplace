import { defaultLocale, getMessages, isLocale, localeLabels, locales, type Locale } from "./messages";

type Flat = Map<string, string>;

function flatten(value: unknown, prefix = "", out: Flat = new Map()): Flat {
  if (typeof value === "string") {
    out.set(prefix, value);
  } else if (typeof value === "object" && value !== null) {
    for (const [key, child] of Object.entries(value)) flatten(child, prefix ? `${prefix}.${key}` : key, out);
  } else {
    throw new Error(`Unexpected catalog value at ${prefix}: ${String(value)}`);
  }
  return out;
}

const catalogs = Object.fromEntries(locales.map((l) => [l, flatten(getMessages(l))])) as Record<Locale, Flat>;
const placeholders = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort();

/** Strings that are legitimately empty or identical in every language. */
const EMPTY_ALLOWED = new Set(["market.cart.issues.None"]);

describe("message catalogs", () => {
  it("has three locales with Azerbaijani as the default and a native label for each", () => {
    expect([...locales]).toEqual(["az", "tr", "en"]);
    expect(defaultLocale()).toBe("az");
    for (const l of locales) expect(localeLabels[l].length).toBeGreaterThan(0);
  });

  it("accepts every supported locale from the cookie (az included) and rejects others", () => {
    for (const l of locales) expect(isLocale(l)).toBe(true);
    for (const bad of ["de", "", "AZ", undefined, null]) expect(isLocale(bad)).toBe(false);
  });

  it.each(locales)("%s has exactly the same keys as the Turkish source catalog", (locale) => {
    const source = [...catalogs.tr.keys()].sort();
    const actual = [...catalogs[locale].keys()].sort();
    expect(actual.filter((k) => !catalogs.tr.has(k))).toEqual([]); // extra keys
    expect(source.filter((k) => !catalogs[locale].has(k))).toEqual([]); // missing keys
  });

  it.each(locales)("%s has no empty text (a missing key can never render blank)", (locale) => {
    const empty = [...catalogs[locale]].filter(
      ([key, text]) => text.trim() === "" && !EMPTY_ALLOWED.has(key),
    );
    expect(empty.map(([key]) => key)).toEqual([]);
  });

  it.each(locales)("%s uses the same {placeholders} as the source for every key", (locale) => {
    const mismatches = [...catalogs.tr]
      .filter(
        ([key, text]) =>
          JSON.stringify(placeholders(text)) !==
          JSON.stringify(placeholders(catalogs[locale].get(key) ?? "")),
      )
      .map(([key]) => key);
    expect(mismatches).toEqual([]);
  });

  it("does not leave English text in the Azerbaijani or Turkish catalogs", () => {
    // A string identical to the English one is almost always an untranslated copy (loan words are allow-listed).
    const allowed = new Set([
      "brand",
      "market.catalog.location", // "{district}, {city}" is language neutral
      "market.orders.status", // "Status" is also the Azerbaijani word
      "admin.shops.statusFilter",
    ]);
    for (const locale of ["az", "tr"] as const) {
      const copies = [...catalogs.en]
        .filter(
          ([key, text]) =>
            catalogs[locale].get(key) === text && !allowed.has(key) && /[A-Za-z]{3,}/.test(text),
        )
        .map(([key, text]) => `${locale}:${key}="${text}"`);
      expect(copies).toEqual([]);
    }
  });

  it("covers every platform role and permission with a label in every language", () => {
    const roles = ["Customer", "FloristOwner", "FloristStaff", "Support", "Finance", "Administrator"];
    const permissions = [
      "system.status.read",
      "shops.manage",
      "categories.manage",
      "orders.read",
      "orders.manage",
      "admin.overview.read",
      "users.read",
    ];
    for (const locale of locales) {
      for (const role of roles) expect(catalogs[locale].has(`admin.roleNames.${role}`)).toBe(true);
      for (const permission of permissions) {
        expect(catalogs[locale].has(`admin.permissionNames.${permission}`)).toBe(true);
      }
    }
  });

  it("covers every order, shop and product status", () => {
    const orderStatuses = [
      "PendingPayment",
      "Paid",
      "Accepted",
      "Preparing",
      "ReadyForDelivery",
      "Delivered",
      "Cancelled",
      "Rejected",
      "Expired",
    ];
    for (const locale of locales) {
      for (const status of orderStatuses)
        expect(catalogs[locale].has(`market.orders.statuses.${status}`)).toBe(true);
      for (const status of ["PendingApproval", "Active", "Inactive", "Suspended", "Rejected"]) {
        expect(catalogs[locale].has(`market.seller.shopStatuses.${status}`)).toBe(true);
      }
    }
  });

  it("uses the right Intl locale per language", () => {
    expect(catalogs.az.get("market.intlLocale")).toBe("az-Latn-AZ");
    expect(catalogs.tr.get("market.intlLocale")).toBe("tr-TR");
    expect(catalogs.en.get("market.intlLocale")).toBe("en-GB");
  });
});
