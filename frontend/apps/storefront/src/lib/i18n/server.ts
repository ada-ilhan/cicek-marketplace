import "server-only";
import { cookies } from "next/headers";
import { defaultLocale, getMessages, isLocale, type Locale, type Messages } from "./messages";

export const LOCALE_COOKIE = "cicek_locale";

/** Locale from the user's preference cookie, else the configured market default. */
export async function getRequestI18n(): Promise<{ locale: Locale; messages: Messages }> {
  const preferred = (await cookies()).get(LOCALE_COOKIE)?.value;
  const locale = isLocale(preferred) ? preferred : defaultLocale();
  return { locale, messages: getMessages(locale) };
}
