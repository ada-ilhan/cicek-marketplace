"use client";

import { ApiError, type OrderDetail } from "@cicek/api-client";
import { Alert, Button, Card, ConfirmDialog, EmptyState, ErrorState, Spinner, TextField } from "@cicek/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState, type FormEvent } from "react";
import { errorMessage } from "@/features/cart/api";
import { OrderDetailPanel, OrderList } from "@/features/orders/OrderComponents";
import { bffFetch } from "@/lib/bff-client";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { adminKeys } from "./api";

export function AdminOrderList() {
  const { messages } = useI18n();
  const t = messages.admin.orders;
  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold">{t.title}</h1>
      <OrderList
        endpoint="/api/bff/v1/admin/orders"
        detailHref={(orderNumber) => `/admin/orders/${orderNumber}`}
        emptyTitle={t.none}
        emptyDescription=""
      />
    </div>
  );
}

/**
 * Administrator order view with the two interventions the domain allows: record a manual payment (clearly labelled: no
 * money is taken) and cancel. Both are audited on the server; the buttons reflect the API's availableActions.
 */
export function AdminOrderDetail({ orderNumber }: { orderNumber: string }) {
  const { messages } = useI18n();
  const t = messages.admin.orders;
  const queryClient = useQueryClient();
  const path = `/api/bff/v1/admin/orders/${encodeURIComponent(orderNumber)}`;
  const queryKey = ["admin", "order", orderNumber];
  const order = useQuery({ queryKey, queryFn: () => bffFetch<OrderDetail>(path) });
  const [reference, setReference] = useState("");
  const [cancelling, setCancelling] = useState(false);
  const [done, setDone] = useState(false);

  const onSuccess = async (updated: OrderDetail) => {
    queryClient.setQueryData(queryKey, updated);
    setCancelling(false);
    setDone(true);
    await queryClient.invalidateQueries({ queryKey: ["orders"] });
    await queryClient.invalidateQueries({ queryKey: adminKeys.overview });
  };
  const pay = useMutation({
    mutationFn: () =>
      bffFetch<OrderDetail>(`${path}/record-payment`, {
        method: "POST",
        body: { reference: reference.trim() },
      }),
    onSuccess,
  });
  const cancel = useMutation({
    mutationFn: (reason: string) =>
      bffFetch<OrderDetail>(`${path}/cancel`, { method: "POST", body: { reason } }),
    onSuccess,
  });

  if (order.isPending) {
    return (
      <Card className="flex justify-center py-16">
        <Spinner label={messages.market.common.loading} />
      </Card>
    );
  }

  if (order.isError) {
    return order.error instanceof ApiError && order.error.kind === "not_found" ? (
      <EmptyState title={messages.market.orders.notFoundTitle} />
    ) : (
      <ErrorState
        title={messages.market.common.loadError}
        onRetry={() => void order.refetch()}
        retryLabel={messages.market.common.retry}
      />
    );
  }

  const actions = order.data.availableActions;
  const onPay = (event: FormEvent) => {
    event.preventDefault();
    setDone(false);
    if (reference.trim()) pay.mutate();
  };

  return (
    <div className="flex flex-col gap-4">
      <Link href="/admin/orders" className="text-sm font-medium text-brand-700 hover:underline">
        ← {t.title}
      </Link>
      <OrderDetailPanel
        order={order.data}
        actions={
          <Card className="flex flex-col gap-4">
            {done ? <Alert tone="success">{t.done}</Alert> : null}
            {actions.includes("record-payment") ? (
              <form onSubmit={onPay} className="flex flex-col gap-3">
                <Alert tone="info" title={t.recordPayment}>
                  {t.paymentWarning}
                </Alert>
                <div className="flex flex-wrap items-end gap-3">
                  <div className="min-w-64 flex-1">
                    <TextField
                      label={t.paymentReference}
                      value={reference}
                      onChange={(e) => setReference(e.target.value)}
                      maxLength={100}
                      required
                    />
                  </div>
                  <Button type="submit" loading={pay.isPending} disabled={!reference.trim()}>
                    {t.recordPayment}
                  </Button>
                </div>
                {pay.isError ? <Alert tone="error">{errorMessage(pay.error, messages.market)}</Alert> : null}
              </form>
            ) : null}
            {actions.includes("cancel") ? (
              <div>
                <Button
                  variant="secondary"
                  onClick={() => {
                    cancel.reset();
                    setDone(false);
                    setCancelling(true);
                  }}
                >
                  {t.cancelOrder}
                </Button>
              </div>
            ) : null}
            {actions.length === 0 ? <p className="text-sm text-ink-600">{t.customerNote}</p> : null}
          </Card>
        }
      />
      <ConfirmDialog
        open={cancelling}
        title={t.cancelOrder}
        confirmLabel={messages.admin.confirm.confirm}
        cancelLabel={messages.admin.confirm.cancel}
        reason={{ label: t.cancelReason, requiredMessage: messages.admin.confirm.reasonRequired }}
        confirmVariant="secondary"
        busy={cancel.isPending}
        error={cancel.isError ? errorMessage(cancel.error, messages.market) : null}
        onCancel={() => setCancelling(false)}
        onConfirm={(reason) => cancel.mutate(reason)}
      >
        {t.cancelBody}
      </ConfirmDialog>
    </div>
  );
}
