"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useId, useState } from "react";
import { AdminNavLink } from "@/components/AdminNavLink";
import { LocaleSwitcher } from "@/components/LocaleSwitcher";
import { useI18n } from "@/lib/i18n/I18nProvider";

const linkClass = "block rounded-lg px-3 py-2 hover:bg-ink-100 md:inline-block";

/**
 * Main navigation. From the `md` breakpoint it is a horizontal bar; on phones it collapses behind a menu button
 * (aria-expanded/aria-controls) that closes on navigation and on Escape.
 */
export function SiteNav({ signedIn }: { signedIn: boolean }) {
  const { messages } = useI18n();
  const t = messages.market.nav;
  const pathname = usePathname();
  const menuId = useId();
  // The menu is "open for" one path, so navigating anywhere closes it without an effect.
  const [openPath, setOpenPath] = useState<string | null>(null);
  const open = openPath === pathname;

  useEffect(() => {
    if (!open) return;
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") setOpenPath(null);
    };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [open]);

  return (
    <nav aria-label={messages.nav.mainLabel} className="w-full md:w-auto">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={menuId}
        onClick={() => setOpenPath(open ? null : pathname)}
        className="absolute right-4 top-3 inline-flex min-h-11 items-center gap-2 rounded-lg border border-ink-200 px-3 text-sm font-medium md:hidden"
      >
        <svg
          viewBox="0 0 24 24"
          aria-hidden="true"
          className="size-5"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
        >
          {open ? <path d="M6 6l12 12M18 6L6 18" /> : <path d="M4 7h16M4 12h16M4 17h16" />}
        </svg>
        {t.menu}
      </button>
      <ul
        id={menuId}
        className={`${open ? "flex" : "hidden"} flex-col gap-1 pt-3 text-sm font-medium md:flex md:flex-row md:flex-wrap md:items-center md:pt-0`}
      >
        <li>
          <Link href="/products" className={linkClass}>
            {t.products}
          </Link>
        </li>
        <li>
          <Link href="/shops" className={linkClass}>
            {t.shops}
          </Link>
        </li>
        {signedIn ? (
          <>
            <li>
              <Link href="/cart" className={linkClass}>
                {t.cart}
              </Link>
            </li>
            <li>
              <Link href="/account/orders" className={linkClass}>
                {t.orders}
              </Link>
            </li>
            <li>
              <Link href="/seller" className={linkClass}>
                {t.seller}
              </Link>
            </li>
            <AdminNavLink />
            <li>
              <Link href="/account" className={linkClass}>
                {messages.nav.account}
              </Link>
            </li>
          </>
        ) : (
          <>
            <li>
              <Link href="/login" className={linkClass}>
                {messages.nav.signIn}
              </Link>
            </li>
            <li>
              <Link href="/register" className={`${linkClass} bg-brand-600 text-white hover:bg-brand-700`}>
                {messages.nav.register}
              </Link>
            </li>
          </>
        )}
        <li className="px-3 py-2 md:p-0">
          <LocaleSwitcher />
        </li>
      </ul>
    </nav>
  );
}
