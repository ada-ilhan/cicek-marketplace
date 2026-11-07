"use client";

import type { OrderStatus, OrderSummary, Paged, ShopStatus } from "@cicek/api-client";
import { Alert, Card, ErrorState, Spinner } from "@cicek/ui";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { OrderStatusBadge, ShopStatusBadge } from "@/components/StatusBadge";
import { bffFetch } from "@/lib/bff-client";
import { formatDateTime, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { useAdminOverview } from "./api";

function Stat({
  label,
  value,
  href,
  tone,
}: {
  label: string;
  value: string;
  href?: string;
  tone?: "warning";
}) {
  const body = (
    <Card className={tone === "warning" ? "border-amber-200 bg-amber-50" : undefined}>
      <p className="text-sm text-ink-600">{label}</p>
      <p className="mt-1 text-3xl font-bold">{value}</p>
    </Card>
  );
  return href ? (
    <Link
      href={href}
      className="block rounded-2xl hover:shadow-md focus-visible:outline-2 focus-visible:outline-brand-600"
    >
      {body}
    </Link>
  ) : (
    body
  );
}

/** Dashboard built from the real /admin/overview endpoint and the latest orders. */
export function AdminOverviewView() {
  const { messages } = useI18n();
  const t = messages.admin.overview;
  const common = messages.market.common;
  const overview = useAdminOverview();
  const recent = useQuery({
    queryKey: ["admin", "recent-orders"],
    queryFn: () => bffFetch<Paged<OrderSummary>>("/api/bff/v1/admin/orders?page=1&pageSize=5"),
  });

  if (overview.isPending) return <Spinner label={common.loading} />;
  if (overview.isError) {
    return (
      <ErrorState
        title={common.loadError}
        onRetry={() => void overview.refetch()}
        retryLabel={common.retry}
      />
    );
  }

  const data = overview.data;
  const money = (amount: number) => formatMoney(amount, data.currency, messages.market.intlLocale);
  const shopCount = (status: ShopStatus) => data.shopsByStatus.find((s) => s.status === status)?.count ?? 0;
  const pending = shopCount("PendingApproval");

  return (
    <div className="flex flex-col gap-8">
      <Alert tone="info">{t.manualPaymentNote}</Alert>

      <section aria-labelledby="shops-heading" className="flex flex-col gap-3">
        <h2 id="shops-heading" className="text-lg font-semibold">
          {t.shopsTitle}
        </h2>
        <div className="grid gap-4 sm:grid-cols-3 lg:grid-cols-5">
          <Stat
            label={t.pendingApplications}
            value={String(pending)}
            href="/admin/shops?status=PendingApproval"
            tone={pending > 0 ? "warning" : undefined}
          />
          {(["Active", "Inactive", "Suspended", "Rejected"] as const).map((status) => (
            <Card key={status}>
              <ShopStatusBadge status={status} labels={messages.market.seller.shopStatuses} />
              <p className="mt-2 text-3xl font-bold">{shopCount(status)}</p>
            </Card>
          ))}
        </div>
        <p>
          <Link
            href="/admin/shops?status=PendingApproval"
            className="text-sm font-medium text-brand-700 hover:underline"
          >
            {t.reviewApplications}
          </Link>
        </p>
      </section>

      <section aria-labelledby="orders-heading" className="flex flex-col gap-3">
        <h2 id="orders-heading" className="text-lg font-semibold">
          {t.ordersTitle}
        </h2>
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label={t.ordersLast7Days} value={String(data.ordersLast7Days)} />
          <Stat label={t.orderValue} value={money(data.orderValue)} />
          <Stat label={t.commission} value={money(data.commissionOnDelivered)} />
          <Stat
            label={t.refundsDue}
            value={money(data.refundsDue)}
            tone={data.refundsDue > 0 ? "warning" : undefined}
          />
          <Stat label={t.activeProducts} value={String(data.activeProducts)} />
          <Stat label={t.users} value={String(data.users)} />
        </div>
        {data.refundsDue > 0 ? <p className="text-sm text-ink-600">{t.refundsNote}</p> : null}
        <ul className="flex flex-wrap gap-2" aria-label={t.ordersTitle}>
          {data.ordersByStatus
            .filter((s) => s.count > 0)
            .map((s) => (
              <li
                key={s.status}
                className="flex items-center gap-2 rounded-lg bg-white px-3 py-2 text-sm shadow-sm"
              >
                <OrderStatusBadge status={s.status as OrderStatus} labels={messages.market.orders.statuses} />
                <strong>{s.count}</strong>
              </li>
            ))}
        </ul>
      </section>

      <section aria-labelledby="recent-heading" className="flex flex-col gap-3">
        <div className="flex items-center justify-between">
          <h2 id="recent-heading" className="text-lg font-semibold">
            {t.recentOrders}
          </h2>
          <Link href="/admin/orders" className="text-sm font-medium text-brand-700 hover:underline">
            {t.viewAll}
          </Link>
        </div>
        {recent.isPending ? (
          <Spinner label={common.loading} />
        ) : recent.isError ? (
          <ErrorState
            title={common.loadError}
            onRetry={() => void recent.refetch()}
            retryLabel={common.retry}
          />
        ) : recent.data.items.length === 0 ? (
          <p className="text-sm text-ink-600">{messages.admin.orders.none}</p>
        ) : (
          <ul className="divide-y divide-ink-100 rounded-2xl border border-ink-100 bg-white">
            {recent.data.items.map((order) => (
              <li
                key={order.orderNumber}
                className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
              >
                <Link
                  href={`/admin/orders/${order.orderNumber}`}
                  className="font-medium text-brand-700 hover:underline"
                >
                  {order.orderNumber}
                </Link>
                <span className="text-ink-600">
                  {order.shopName} · {formatDateTime(order.createdAt, messages.market.intlLocale)}
                </span>
                <OrderStatusBadge status={order.status} labels={messages.market.orders.statuses} />
                <span className="font-semibold">
                  {formatMoney(order.total, order.currency, messages.market.intlLocale)}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}
