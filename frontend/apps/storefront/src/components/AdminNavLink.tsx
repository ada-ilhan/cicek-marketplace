"use client";

import Link from "next/link";
import { useCurrentUser } from "@/features/auth/api";
import { useI18n } from "@/lib/i18n/I18nProvider";

/** Shows the administration link only to users the API reports as having administrative permissions (UX only). */
export function AdminNavLink() {
  const me = useCurrentUser();
  const { messages } = useI18n();
  if (!me.data?.permissions.includes("admin.overview.read")) return null;
  return (
    <li>
      <Link href="/admin" className="rounded-lg px-3 py-2 hover:bg-ink-100">
        {messages.market.nav.admin}
      </Link>
    </li>
  );
}
