"use client";

import { useRouter } from "next/navigation";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { localeLabels, locales, type Locale } from "@/lib/i18n/messages";

/** Cookie read by the server (lib/i18n/server.ts LOCALE_COOKIE). A UI preference only, not security-relevant. */
const LOCALE_COOKIE = "cicek_locale";

export function LocaleSwitcher() {
  const { locale, messages } = useI18n();
  const router = useRouter();

  function change(next: Locale) {
    document.cookie = `${LOCALE_COOKIE}=${next}; path=/; max-age=31536000; samesite=lax`;
    router.refresh();
  }

  return (
    <label className="flex items-center gap-2 text-sm">
      <span className="sr-only">{messages.market.nav.language}</span>
      <select
        value={locale}
        onChange={(e) => change(e.target.value as Locale)}
        className="min-h-9 rounded-lg border border-ink-200 bg-white px-2"
      >
        {locales.map((l) => (
          <option key={l} value={l} lang={l}>
            {localeLabels[l]}
          </option>
        ))}
      </select>
    </label>
  );
}
