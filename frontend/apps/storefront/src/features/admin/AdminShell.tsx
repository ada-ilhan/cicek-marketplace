"use client";

import { ApiError } from "@cicek/api-client";
import { Card, EmptyState, ErrorState, Spinner } from "@cicek/ui";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { useCurrentUser } from "@/features/auth/api";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { adminPermissions } from "./permissions";

/**
 * Administrator area frame: loads the caller's permissions, shows an access-denied page when the required permission is
 * missing (or the session is gone), and renders navigation limited to what the caller may use. This is a usability
 * layer only; every API call is authorized again on the server.
 */
export function AdminShell({ permission, children }: { permission: string; children: ReactNode }) {
  const { messages } = useI18n();
  const t = messages.admin;
  const pathname = usePathname();
  const me = useCurrentUser();

  if (me.isPending) {
    return (
      <Card className="flex justify-center py-16">
        <Spinner label={messages.market.common.loading} />
      </Card>
    );
  }

  if (me.isError && !(me.error instanceof ApiError && me.error.kind === "unauthorized")) {
    return (
      <ErrorState
        title={messages.market.common.loadError}
        onRetry={() => void me.refetch()}
        retryLabel={messages.market.common.retry}
      />
    );
  }

  const permissions = me.data?.permissions ?? [];
  if (me.isError || !permissions.includes(permission)) {
    return (
      <EmptyState
        title={t.accessDenied.title}
        description={t.accessDenied.description}
        action={
          <Link
            href={`/login?next=${encodeURIComponent(pathname)}`}
            className="font-medium text-brand-700 hover:underline"
          >
            {t.accessDenied.signIn}
          </Link>
        }
      />
    );
  }

  const links = [
    { href: "/admin", label: t.nav.overview, permission: adminPermissions.overview },
    { href: "/admin/shops", label: t.nav.shops, permission: adminPermissions.shops },
    { href: "/admin/orders", label: t.nav.orders, permission: adminPermissions.orders },
    { href: "/admin/categories", label: t.nav.categories, permission: adminPermissions.categories },
    { href: "/admin/users", label: t.nav.users, permission: adminPermissions.users },
  ].filter((link) => permissions.includes(link.permission));

  return (
    <div className="flex flex-col gap-6">
      <nav aria-label={t.title} className="flex flex-wrap items-center gap-1 border-b border-ink-100 pb-3">
        <span className="mr-3 text-xl font-bold">{t.title}</span>
        {links.map((link) => {
          const current = link.href === "/admin" ? pathname === "/admin" : pathname.startsWith(link.href);
          return (
            <Link
              key={link.href}
              href={link.href}
              aria-current={current ? "page" : undefined}
              className={`rounded-lg px-3 py-2 text-sm font-medium hover:bg-ink-100 ${current ? "bg-brand-50 text-brand-900" : ""}`}
            >
              {link.label}
            </Link>
          );
        })}
      </nav>
      {children}
    </div>
  );
}
