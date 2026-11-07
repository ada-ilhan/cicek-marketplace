"use client";

import { ApiError, type OrderDetail } from "@cicek/api-client";
import { Alert, Button, Card, ConfirmDialog, EmptyState, ErrorState, Spinner } from "@cicek/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { errorMessage } from "@/features/cart/api";
import { OrderDetailPanel, OrderList } from "@/features/orders/OrderComponents";
import { bffFetch } from "@/lib/bff-client";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { shopPath } from "./api";
import { ShopGate, ShopNav } from "./ShopDashboard";

export function ShopOrders({ shopId }: { shopId: string }) {
  const t = useI18n().messages.market.seller;
  return (
    <ShopGate shopId={shopId}>
      {(shop) => (
        <>
          <ShopNav shop={shop} />
          <OrderList
            endpoint={`${shopPath(shopId)}/orders`}
            detailHref={(orderNumber) => `/seller/shops/${shopId}/orders/${orderNumber}`}
            emptyTitle={t.noOrdersTitle}
            emptyDescription={t.noOrdersDescription}
          />
        </>
      )}
    </ShopGate>
  );
}

type ShopAction = "accept" | "reject" | "start-preparation" | "mark-ready" | "mark-delivered";
type ConfirmedAction = "accept" | "reject" | "mark-delivered";

/** Actions that cannot be taken back or commit the florist: they ask for confirmation first. */
const isConfirmed = (action: ShopAction): action is ConfirmedAction =>
  action === "accept" || action === "reject" || action === "mark-delivered";

/** Shop's view of an order: only the actions the API reports as available are offered. */
export function ShopOrderDetail({ shopId, orderNumber }: { shopId: string; orderNumber: string }) {
  const t = useI18n().messages.market;
  const queryClient = useQueryClient();
  const path = `${shopPath(shopId)}/orders/${encodeURIComponent(orderNumber)}`;
  const queryKey = ["orders", "shop-detail", shopId, orderNumber];
  const order = useQuery({ queryKey, queryFn: () => bffFetch<OrderDetail>(path) });
  const [confirming, setConfirming] = useState<ConfirmedAction | null>(null);
  const act = useMutation({
    mutationFn: ({ action, reason }: { action: ShopAction; reason?: string }) =>
      bffFetch<OrderDetail>(`${path}/${action}`, {
        method: "POST",
        ...(action === "reject" ? { body: { reason } } : {}),
      }),
    onSuccess: async (updated) => {
      setConfirming(null);
      queryClient.setQueryData(queryKey, updated);
      await queryClient.invalidateQueries({ queryKey: ["orders"] });
    },
  });

  return (
    <ShopGate shopId={shopId}>
      {(shop) => (
        <>
          <ShopNav shop={shop} />
          {order.isPending ? (
            <Card className="flex justify-center py-16">
              <Spinner label={t.common.loading} />
            </Card>
          ) : order.isError ? (
            order.error instanceof ApiError && order.error.kind === "not_found" ? (
              <EmptyState title={t.orders.notFoundTitle} />
            ) : (
              <ErrorState
                title={t.common.loadError}
                onRetry={() => void order.refetch()}
                retryLabel={t.common.retry}
              />
            )
          ) : (
            <OrderDetailPanel
              order={order.data}
              actions={
                <Card className="flex flex-col gap-3">
                  {order.data.status === "PendingPayment" ? (
                    <Alert tone="info">{t.seller.awaitingPayment}</Alert>
                  ) : null}
                  {act.isError ? <Alert tone="error">{errorMessage(act.error, t)}</Alert> : null}
                  <div className="flex flex-wrap items-end gap-3">
                    {(order.data.availableActions as ShopAction[]).map((action) => (
                      <Button
                        key={action}
                        variant={action === "reject" ? "secondary" : "primary"}
                        loading={act.isPending && act.variables?.action === action}
                        disabled={act.isPending}
                        onClick={() => (isConfirmed(action) ? setConfirming(action) : act.mutate({ action }))}
                      >
                        {t.seller.actions[action]}
                      </Button>
                    ))}
                  </div>
                  {confirming ? (
                    <ConfirmDialog
                      open
                      title={t.seller.confirm[confirming].title}
                      confirmLabel={t.seller.actions[confirming]}
                      cancelLabel={t.common.cancel}
                      confirmVariant={confirming === "reject" ? "secondary" : "primary"}
                      busy={act.isPending}
                      error={act.isError ? errorMessage(act.error, t) : null}
                      {...(confirming === "reject"
                        ? {
                            reason: {
                              label: t.seller.rejectReason,
                              requiredMessage: t.checkout.errors.required,
                            },
                          }
                        : {})}
                      onConfirm={(reason) => act.mutate({ action: confirming, reason })}
                      onCancel={() => {
                        setConfirming(null);
                        act.reset();
                      }}
                    >
                      {t.seller.confirm[confirming].body}
                    </ConfirmDialog>
                  ) : null}
                </Card>
              }
            />
          )}
        </>
      )}
    </ShopGate>
  );
}
