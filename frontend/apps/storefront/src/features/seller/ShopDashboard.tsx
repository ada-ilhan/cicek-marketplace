"use client";

import { ApiError, type ShopManagement } from "@cicek/api-client";
import { Alert, Button, Card, EmptyState, ErrorState, Spinner } from "@cicek/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import type { ReactNode } from "react";
import { SafeImage } from "@/components/SafeImage";
import { ShopStatusBadge } from "@/components/StatusBadge";
import { errorMessage } from "@/features/cart/api";
import { bffFetch } from "@/lib/bff-client";
import { format } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { sellerKeys, shopPath, useMyShop, useShopProducts } from "./api";
import { ShopProfileEditor } from "./ShopProfileEditor";

/** Loads the shop (API enforces membership) and renders children with it; handles loading/403/404/error. */
export function ShopGate({
  shopId,
  children,
}: {
  shopId: string;
  children: (shop: ShopManagement) => ReactNode;
}) {
  const t = useI18n().messages.market;
  const shop = useMyShop(shopId);
  if (shop.isPending) {
    return (
      <Card className="flex justify-center py-16">
        <Spinner label={t.common.loading} />
      </Card>
    );
  }

  if (shop.isError) {
    const kind = shop.error instanceof ApiError ? shop.error.kind : "server";
    return kind === "forbidden" || kind === "not_found" ? (
      <EmptyState title={t.catalog.noShopsTitle} />
    ) : (
      <ErrorState
        title={t.common.loadError}
        onRetry={() => void shop.refetch()}
        retryLabel={t.common.retry}
      />
    );
  }

  return <>{children(shop.data)}</>;
}

export function ShopNav({ shop }: { shop: ShopManagement }) {
  const t = useI18n().messages.market.seller;
  const link = "rounded-lg px-3 py-2 text-sm font-medium hover:bg-ink-100";
  return (
    <nav aria-label={shop.name} className="mb-6 flex flex-wrap items-center gap-2">
      <Link href={`/seller/shops/${shop.id}`} className="mr-2 text-xl font-bold">
        {shop.name}
      </Link>
      <Link href={`/seller/shops/${shop.id}/products`} className={link}>
        {t.products}
      </Link>
      <Link href={`/seller/shops/${shop.id}/orders`} className={link}>
        {t.orders}
      </Link>
    </nav>
  );
}

export function ShopDashboard({ shopId }: { shopId: string }) {
  const t = useI18n().messages.market;
  const queryClient = useQueryClient();
  const toggle = useMutation({
    mutationFn: (open: boolean) =>
      bffFetch<ShopManagement>(`${shopPath(shopId)}/${open ? "open" : "close"}`, { method: "POST" }),
    onSuccess: async (shop) => {
      queryClient.setQueryData(sellerKeys.shop(shopId), shop);
      await queryClient.invalidateQueries({ queryKey: sellerKeys.shops });
    },
  });

  const resubmit = useMutation({
    mutationFn: () => bffFetch<ShopManagement>(`${shopPath(shopId)}/resubmit`, { method: "POST" }),
    onSuccess: async (shop) => {
      queryClient.setQueryData(sellerKeys.shop(shopId), shop);
      await queryClient.invalidateQueries({ queryKey: sellerKeys.shops });
    },
  });

  return (
    <ShopGate shopId={shopId}>
      {(shop) => (
        <>
          <ShopNav shop={shop} />
          <Card className="flex flex-col gap-4">
            <div className="flex items-center gap-3">
              <div className="size-14 shrink-0 overflow-hidden rounded-xl bg-ink-100">
                <SafeImage
                  src={shop.imageUrl}
                  alt=""
                  fallbackLabel=""
                  className="[&_span]:hidden [&_svg]:size-6"
                />
              </div>
              <ShopStatusBadge status={shop.status} labels={t.seller.shopStatuses} />
              <span className="text-sm text-ink-600">/{shop.slug}</span>
            </div>
            {shop.status === "PendingApproval" ? <Alert tone="info">{t.seller.pendingNotice}</Alert> : null}
            {shop.status === "Rejected" ? (
              <Alert tone="error">
                {format(t.seller.rejectedNotice, { reason: shop.statusReason ?? "" })}
              </Alert>
            ) : null}
            {shop.status === "Inactive" ? <Alert tone="info">{t.seller.inactiveNotice}</Alert> : null}
            {shop.status === "Suspended" ? (
              <Alert tone="error">
                {format(t.seller.suspendedNotice, { reason: shop.statusReason ?? "" })}
              </Alert>
            ) : null}
            {toggle.isError ? <Alert tone="error">{errorMessage(toggle.error, t)}</Alert> : null}
            {resubmit.isError ? <Alert tone="error">{errorMessage(resubmit.error, t)}</Alert> : null}
            {shop.myRole === "Owner" && shop.status === "Rejected" ? (
              <div>
                <Button loading={resubmit.isPending} onClick={() => resubmit.mutate()}>
                  {t.seller.resubmit}
                </Button>
              </div>
            ) : null}
            {shop.myRole === "Owner" && shop.status === "Active" ? (
              <div>
                <Button variant="secondary" loading={toggle.isPending} onClick={() => toggle.mutate(false)}>
                  {t.seller.close}
                </Button>
              </div>
            ) : null}
            {shop.myRole === "Owner" && shop.status === "Inactive" ? (
              <div>
                <Button loading={toggle.isPending} onClick={() => toggle.mutate(true)}>
                  {t.seller.open}
                </Button>
              </div>
            ) : null}
          </Card>
          <ShopOverview shopId={shopId} />
          {shop.myRole === "Owner" ? <ShopProfileEditor shop={shop} /> : null}
        </>
      )}
    </ShopGate>
  );
}

function StatCard({ label, value, href }: { label: string; value: number | undefined; href: string }) {
  return (
    <Link
      href={href}
      className="flex flex-col gap-1 rounded-2xl border border-ink-100 bg-white p-5 shadow-sm hover:border-brand-200 focus-visible:outline-2 focus-visible:outline-brand-600"
    >
      <span className="text-3xl font-bold text-ink-900">{value ?? "—"}</span>
      <span className="text-sm text-ink-600">{label}</span>
    </Link>
  );
}

/** Real counts from the API (no placeholders): products on sale, sold-out products and orders waiting for acceptance. */
function ShopOverview({ shopId }: { shopId: string }) {
  const t = useI18n().messages.market.seller;
  const active = useShopProducts(shopId, 1, { status: "Active", pageSize: 1 });
  const soldOut = useShopProducts(shopId, 1, { status: "Active", outOfStock: true, pageSize: 1 });
  const awaiting = useQuery({
    queryKey: ["orders", "shop-awaiting", shopId],
    queryFn: () =>
      bffFetch<{ totalCount: number }>(`${shopPath(shopId)}/orders?status=Paid&page=1&pageSize=1`),
  });
  return (
    <section aria-labelledby="shop-overview" className="my-6">
      <h2 id="shop-overview" className="mb-3 text-lg font-semibold">
        {t.overviewTitle}
      </h2>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard
          label={t.stats.activeProducts}
          value={active.data?.totalCount}
          href={`/seller/shops/${shopId}/products`}
        />
        <StatCard
          label={t.stats.outOfStock}
          value={soldOut.data?.totalCount}
          href={`/seller/shops/${shopId}/products`}
        />
        <StatCard
          label={t.stats.awaitingAcceptance}
          value={awaiting.data?.totalCount}
          href={`/seller/shops/${shopId}/orders`}
        />
      </div>
    </section>
  );
}
