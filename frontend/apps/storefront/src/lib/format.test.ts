/** Intl separates amount and currency with a non-breaking space; compare with a plain one. */
const plain = (text: string) => text.replace(/[  ]/g, " ");

import { format, formatDate, formatDateTime, formatMoney, formatNumber } from "./format";

describe("format helpers", () => {
  it("fills placeholders and leaves unknown ones", () => {
    expect(format("{count} ürün, {x}", { count: 3 })).toBe("3 ürün, {x}");
  });

  it("formats money with the currency from the data (explicit multi-currency behaviour)", () => {
    expect(formatMoney(1234.5, "TRY", "tr-TR")).toMatch(/1\.234,50/);
    expect(formatMoney(1234.5, "EUR", "en-GB")).toBe("€1,234.50");
  });

  it("formats Azerbaijani manats with the manat sign and Azerbaijani separators", () => {
    // Node ships full ICU; the exact string is also verified in a real browser by the Playwright locale spec.
    expect(plain(formatMoney(1234.5, "AZN", "az-Latn-AZ"))).toBe("1.234,50 ₼");
    expect(plain(formatMoney(49.9, "AZN", "az-Latn-AZ"))).toBe("49,90 ₼");
  });

  it("renders Azerbaijani dates and times with Azerbaijani month abbreviations, in the market time zone", () => {
    expect(formatDate("2026-10-14", "az-Latn-AZ")).toBe("14 okt 2026");
    expect(formatDate("2026-01-05", "az-Latn-AZ")).toBe("5 yan 2026");
    expect(formatDateTime("2026-10-14T20:30:00Z", "az-Latn-AZ")).toBe("15 okt 2026, 00:30");
    expect(formatDateTime("2026-12-31T23:59:00Z", "az-Latn-AZ", "UTC")).toBe("31 dek 2026, 23:59");
  });

  it("matches Node's full-ICU Azerbaijani output, so server and browser renderings agree", () => {
    const icu = new Intl.DateTimeFormat("az-Latn-AZ", {
      dateStyle: "medium",
      timeStyle: "short",
      timeZone: "Asia/Baku",
    })
      .format(new Date("2026-10-14T20:30:00Z"))
      .replace(/[  ]/g, " ");
    expect(formatDateTime("2026-10-14T20:30:00Z", "az-Latn-AZ")).toBe(icu);
  });

  it("never mixes currencies: the symbol follows the amount's own currency", () => {
    expect(plain(formatMoney(10, "AZN", "en-GB"))).toBe("AZN 10.00");
    expect(formatMoney(10, "AZN", "tr-TR")).toContain("AZN");
  });

  it("formats calendar dates without time-zone shifts", () => {
    expect(formatDate("2026-02-14", "en-GB")).toBe("14 Feb 2026");
  });

  it("shows instants in the market time zone (Asia/Baku, UTC+4) regardless of the machine's zone", () => {
    expect(plain(formatDateTime("2026-10-14T20:30:00Z", "en-GB"))).toBe("15 Oct 2026, 00:30");
    expect(plain(formatDateTime("2026-10-14T20:30:00Z", "en-GB", "Europe/Istanbul"))).toBe(
      "14 Oct 2026, 23:30",
    );
  });

  it("formats plain numbers with the locale's decimal separator", () => {
    expect(formatNumber(12.5, "az-Latn-AZ")).toBe("12,5");
    expect(formatNumber(12.5, "en-GB")).toBe("12.5");
  });
});
