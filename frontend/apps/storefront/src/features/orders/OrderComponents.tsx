"use client";

import type { OrderDetail, OrderStatus, OrderSummary, Paged } from "@cicek/api-client";
import { Card, EmptyState, ErrorState, SelectField, Spinner } from "@cicek/ui";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState, type ReactNode } from "react";
import { DataTable } from "@/components/DataTable";
import { PagerButtons } from "@/components/PagerButtons";
import { OrderStatusBadge } from "@/components/StatusBadge";
import { bffFetch } from "@/lib/bff-client";
import { format, formatDate, formatDateTime, formatMoney, formatNumber } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";

export const orderStatuses: OrderStatus[] = [
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

/**
 * Paged, filterable order list. `endpoint` is the BFF path of the list (customer or shop scope); the API
 * decides what the caller may see.
 */
export function OrderList({
  endpoint,
  detailHref,
  emptyTitle,
  emptyDescription,
}: {
  endpoint: string;
  detailHref: (orderNumber: string) => string;
  emptyTitle: string;
  emptyDescription: string;
}) {
  const t = useI18n().messages.market;
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState<OrderStatus | "">("");
  const query = useQuery({
    queryKey: ["orders", endpoint, page, status],
    queryFn: () =>
      bffFetch<Paged<OrderSummary>>(
        `${endpoint}?${new URLSearchParams({ page: String(page), pageSize: "10", ...(status ? { status } : {}) })}`,
      ),
  });

  return (
    <div className="flex flex-col gap-4">
      <div className="max-w-xs">
        <SelectField
          label={t.orders.statusFilter}
          value={status}
          onChange={(e) => {
            setStatus(e.target.value as OrderStatus | "");
            setPage(1);
          }}
        >
          <option value="">{t.common.all}</option>
          {orderStatuses.map((s) => (
            <option key={s} value={s}>
              {t.orders.statuses[s]}
            </option>
          ))}
        </SelectField>
      </div>
      {query.isPending ? (
        <Card className="flex justify-center py-16">
          <Spinner label={t.common.loading} />
        </Card>
      ) : query.isError ? (
        <ErrorState
          title={t.common.loadError}
          onRetry={() => void query.refetch()}
          retryLabel={t.common.retry}
        />
      ) : query.data.totalCount === 0 ? (
        <EmptyState title={emptyTitle} description={emptyDescription} />
      ) : (
        <>
          <DataTable
            caption={t.orders.title}
            rows={query.data.items}
            rowKey={(order) => order.orderNumber}
            rowClassName={(order) => (order.status === "PendingPayment" ? "bg-warning-50" : undefined)}
            columns={[
              {
                key: "number",
                header: t.orders.number,
                primary: true,
                cell: (order) => (
                  <Link
                    href={detailHref(order.orderNumber)}
                    className="font-medium text-brand-700 hover:underline"
                  >
                    {order.orderNumber}
                  </Link>
                ),
              },
              {
                key: "date",
                header: t.orders.date,
                cell: (order) => formatDateTime(order.createdAt, t.intlLocale),
              },
              { key: "shop", header: t.orders.shop, cell: (order) => order.shopName },
              {
                key: "delivery",
                header: t.orders.deliveryDate,
                cell: (order) => formatDate(order.deliveryDate, t.intlLocale),
              },
              {
                key: "status",
                header: t.orders.status,
                cell: (order) => <OrderStatusBadge status={order.status} labels={t.orders.statuses} />,
              },
              {
                key: "total",
                header: t.orders.total,
                align: "right",
                cell: (order) => (
                  <span className="font-semibold">
                    {formatMoney(order.total, order.currency, t.intlLocale)}
                  </span>
                ),
              },
            ]}
          />
          <PagerButtons page={query.data.page} totalPages={query.data.totalPages} onChange={setPage} />
        </>
      )}
    </div>
  );
}

/** Read-only order presentation shared by the customer and seller views; `actions` renders the allowed buttons. */
export function OrderDetailPanel({ order, actions }: { order: OrderDetail; actions?: ReactNode }) {
  const t = useI18n().messages.market;
  const money = (amount: number) => formatMoney(amount, order.currency, t.intlLocale);
  return (
    <div className="flex flex-col gap-6">
      <Card className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold">{order.orderNumber}</h1>
          <p className="text-ink-600">
            {order.shopName} · {formatDateTime(order.createdAt, t.intlLocale)}
          </p>
        </div>
        <OrderStatusBadge status={order.status} labels={t.orders.statuses} />
      </Card>

      <Card className="flex flex-col gap-1 text-sm">
        <p>
          <strong>{t.orders.paymentTitle}:</strong>{" "}
          {order.paidAt ? t.orders.paymentRecorded : t.orders.paymentPending}
        </p>
        <p className="text-ink-600">{t.orders.paymentNote}</p>
        {(["Accepted", "Preparing", "ReadyForDelivery", "Delivered"] as const).some(
          (s) => s === order.status,
        ) ? (
          <p className="text-ink-600">{t.orders.deliveryNote}</p>
        ) : null}
      </Card>

      {actions}

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <h2 className="mb-3 font-semibold">{t.orders.items}</h2>
          <ul className="divide-y divide-ink-100">
            {order.items.map((item) => (
              <li key={item.productId} className="flex justify-between gap-4 py-2 text-sm">
                <span>
                  {item.quantity} × {item.productName}
                </span>
                <span className="font-medium">{money(item.lineTotal)}</span>
              </li>
            ))}
          </ul>
          <dl className="mt-4 grid grid-cols-2 gap-1 text-sm">
            <dt className="text-ink-600">{t.cart.subtotal}</dt>
            <dd className="text-right">{money(order.subtotal)}</dd>
            <dt className="text-ink-600">{t.cart.deliveryFee}</dt>
            <dd className="text-right">{money(order.deliveryFee)}</dd>
            <dt className="font-semibold">{t.orders.total}</dt>
            <dd className="text-right font-semibold">{money(order.total)}</dd>
            {order.refundDue > 0 ? (
              <>
                <dt className="text-danger-700">{t.orders.refundDue}</dt>
                <dd className="text-right text-danger-700">{money(order.refundDue)}</dd>
              </>
            ) : null}
            {order.financials ? (
              <>
                <dt className="text-ink-600">
                  {format(t.seller.commission, {
                    rate: formatNumber(order.financials.commissionRateBps / 100, t.intlLocale),
                  })}
                </dt>
                <dd className="text-right">−{money(order.financials.commission)}</dd>
                <dt className="font-semibold">{t.seller.earnings}</dt>
                <dd className="text-right font-semibold">{money(order.financials.shopEarnings)}</dd>
              </>
            ) : null}
          </dl>
        </Card>
        <Card>
          <h2 className="mb-3 font-semibold">{t.orders.delivery}</h2>
          <address className="text-sm not-italic">
            <strong>{order.delivery.recipientName}</strong>
            <br />
            {order.delivery.recipientPhone}
            <br />
            {order.delivery.addressLine}
            <br />
            {order.delivery.district}, {order.delivery.city} {order.delivery.postalCode ?? ""}
          </address>
          <p className="mt-2 text-sm">
            {t.orders.deliveryDate}: <strong>{formatDate(order.delivery.deliveryDate, t.intlLocale)}</strong>
          </p>
          {order.delivery.giftMessage ? (
            <blockquote className="mt-3 border-l-4 border-brand-200 pl-3 text-sm italic">
              {order.delivery.giftMessage}
            </blockquote>
          ) : null}
        </Card>
      </div>

      <Card>
        <h2 className="mb-3 font-semibold">{t.orders.timeline}</h2>
        <ol className="flex flex-col gap-2 text-sm">
          {order.history.map((change, index) => (
            <li key={`${change.occurredAt}-${index}`} className="flex flex-wrap gap-2">
              <span className="text-ink-600">{formatDateTime(change.occurredAt, t.intlLocale)}</span>
              <OrderStatusBadge status={change.to} labels={t.orders.statuses} />
              <span className="text-ink-600">({t.orders.actors[change.actor]})</span>
              {change.reason ? <span>— {change.reason}</span> : null}
            </li>
          ))}
        </ol>
      </Card>
    </div>
  );
}
