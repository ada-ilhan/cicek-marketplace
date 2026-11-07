/** Locale-aware formatting. Currency and locale always come from data/configuration, never constants. */

export function format(template: string, values: Record<string, string | number>): string {
  return template.replace(/\{(\w+)\}/g, (match, key: string) =>
    key in values ? String(values[key]) : match,
  );
}

/**
 * Azerbaijani (az) is formatted by this module instead of the platform's Intl data. Real Chrome and Edge ship only partial
 * "az" data (amounts come out as "AZN 1,234.50" and dates as "2026 M10 14"), while Node's full ICU is correct, which would
 * also make server and browser renderings differ. Turkish uses the same separators as Azerbaijani (".": groups, ",": decimals)
 * and its data is complete everywhere, so it supplies the digits; symbols and month names are CLDR's Azerbaijani ones.
 */
const isAzerbaijani = (intlLocale: string) => /^az(-|$)/i.test(intlLocale);

const AZ_CURRENCY_SYMBOLS: Record<string, string> = { AZN: "₼", TRY: "₺", USD: "$", EUR: "€", GBP: "£" };
const AZ_MONTHS = [
  "yan",
  "fev",
  "mar",
  "apr",
  "may",
  "iyn",
  "iyl",
  "avq",
  "sen",
  "okt",
  "noy",
  "dek",
] as const;

function azNumber(value: number, fractionDigits: { min: number; max: number }): string {
  return new Intl.NumberFormat("tr-TR", {
    minimumFractionDigits: fractionDigits.min,
    maximumFractionDigits: fractionDigits.max,
  }).format(value);
}

export function formatMoney(amount: number, currency: string, intlLocale: string): string {
  if (isAzerbaijani(intlLocale)) {
    const digits =
      new Intl.NumberFormat("en", { style: "currency", currency }).resolvedOptions().maximumFractionDigits ??
      2;
    return `${azNumber(amount, { min: digits, max: digits })} ${AZ_CURRENCY_SYMBOLS[currency] ?? currency}`;
  }
  return new Intl.NumberFormat(intlLocale, { style: "currency", currency }).format(amount);
}

/** Formats an ISO date ("2026-10-14") as a calendar date without shifting it through time zones. */
export function formatDate(isoDate: string, intlLocale: string): string {
  const [year, month, day] = isoDate.split("-").map(Number);
  if (isAzerbaijani(intlLocale)) return `${day} ${AZ_MONTHS[(month ?? 1) - 1]} ${year}`;
  return new Intl.DateTimeFormat(intlLocale, { dateStyle: "medium", timeZone: "UTC" }).format(
    new Date(Date.UTC(year ?? 1970, (month ?? 1) - 1, day ?? 1)),
  );
}

/**
 * Market time zone for instants (order times, registration times). Fixed on purpose: the server render and the browser
 * must agree (no hydration mismatch) and a customer abroad still sees Baku time. Configurable for other markets.
 */
export const MARKET_TIME_ZONE = process.env.NEXT_PUBLIC_MARKET_TIME_ZONE ?? "Asia/Baku";

export function formatDateTime(
  isoInstant: string,
  intlLocale: string,
  timeZone: string = MARKET_TIME_ZONE,
): string {
  if (isAzerbaijani(intlLocale)) {
    const parts = Object.fromEntries(
      new Intl.DateTimeFormat("en-US", {
        timeZone,
        year: "numeric",
        month: "numeric",
        day: "numeric",
        hour: "2-digit",
        minute: "2-digit",
        hourCycle: "h23",
      })
        .formatToParts(new Date(isoInstant))
        .map((part) => [part.type, part.value]),
    ) as Record<string, string>;
    return `${Number(parts.day)} ${AZ_MONTHS[Number(parts.month) - 1]} ${parts.year}, ${parts.hour}:${parts.minute}`;
  }
  return new Intl.DateTimeFormat(intlLocale, { dateStyle: "medium", timeStyle: "short", timeZone }).format(
    new Date(isoInstant),
  );
}

/** Plain number in the locale's conventions (e.g. a commission percentage: 12,5 in Azerbaijani, 12.5 in English). */
export function formatNumber(value: number, intlLocale: string): string {
  if (isAzerbaijani(intlLocale)) return azNumber(value, { min: 0, max: 2 });
  return new Intl.NumberFormat(intlLocale, { maximumFractionDigits: 2 }).format(value);
}
