"use client";

import { ApiError, type OrderDetail } from "@cicek/api-client";
import { Alert, Button, Card, EmptyState, ErrorState, Spinner, TextField } from "@cicek/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { errorMessage } from "@/features/cart/api";
import { bffFetch } from "@/lib/bff-client";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { OrderDetailPanel, OrderList } from "./OrderComponents";

export function CustomerOrderList() {
  const t = useI18n().messages.market.orders;
  return (
    <OrderList
      endpoint="/api/bff/v1/me/orders"
      detailHref={(orderNumber) => `/account/orders/${orderNumber}`}
      emptyTitle={t.emptyTitle}
      emptyDescription={t.emptyDescription}
    />
  );
}

/** Customer's view of one order; offers cancellation while the API allows it. */
export function CustomerOrderDetail({ orderNumber }: { orderNumber: string }) {
  const t = useI18n().messages.market;
  const queryClient = useQueryClient();
  const path = `/api/bff/v1/me/orders/${encodeURIComponent(orderNumber)}`;
  const queryKey = ["orders", "detail", orderNumber];
  const order = useQuery({ queryKey, queryFn: () => bffFetch<OrderDetail>(path) });
  const [reason, setReason] = useState("");
  const cancel = useMutation({
    mutationFn: () => bffFetch<OrderDetail>(`${path}/cancel`, { method: "POST", body: { reason } }),
    onSuccess: async (updated) => {
      queryClient.setQueryData(queryKey, updated);
      await queryClient.invalidateQueries({ queryKey: ["orders"] });
    },
  });

  if (order.isPending) {
    return (
      <Card className="flex justify-center py-16">
        <Spinner label={t.common.loading} />
      </Card>
    );
  }

  if (order.isError) {
    return order.error instanceof ApiError && order.error.kind === "not_found" ? (
      <EmptyState title={t.orders.notFoundTitle} />
    ) : (
      <ErrorState
        title={t.common.loadError}
        onRetry={() => void order.refetch()}
        retryLabel={t.common.retry}
      />
    );
  }

  const canCancel = order.data.availableActions.includes("cancel");
  const onCancel = (event: FormEvent) => {
    event.preventDefault();
    if (reason.trim()) cancel.mutate();
  };

  return (
    <OrderDetailPanel
      order={order.data}
      actions={
        canCancel ? (
          <Card>
            <form onSubmit={onCancel} className="flex flex-wrap items-end gap-3">
              <div className="min-w-64 flex-1">
                <TextField
                  label={t.orders.cancelReason}
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                  maxLength={500}
                  required
                />
              </div>
              <Button type="submit" variant="secondary" loading={cancel.isPending} disabled={!reason.trim()}>
                {t.orders.cancelConfirm}
              </Button>
            </form>
            {cancel.isError ? (
              <div className="mt-3">
                <Alert tone="error">{errorMessage(cancel.error, t)}</Alert>
              </div>
            ) : null}
          </Card>
        ) : null
      }
    />
  );
}
