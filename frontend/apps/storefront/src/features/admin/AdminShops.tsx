"use client";

import { ApiError, type AdminShop, type ShopManagement, type ShopStatus } from "@cicek/api-client";
import {
  Alert,
  Button,
  Card,
  ConfirmDialog,
  EmptyState,
  ErrorState,
  SelectField,
  Spinner,
  TextField,
} from "@cicek/ui";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useState, type FormEvent } from "react";
import { DataTable } from "@/components/DataTable";
import { PagerButtons } from "@/components/PagerButtons";
import { ShopStatusBadge } from "@/components/StatusBadge";
import { errorMessage } from "@/features/cart/api";
import { bffFetch } from "@/lib/bff-client";
import { formatDateTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { adminKeys, adminShopPath, useAdminShop, useAdminShops } from "./api";

const statuses: ShopStatus[] = ["PendingApproval", "Active", "Inactive", "Suspended", "Rejected"];

export function AdminShopList() {
  const { messages } = useI18n();
  const t = messages.admin.shops;
  const initial = useSearchParams().get("status");
  const [status, setStatus] = useState<ShopStatus | "">(
    statuses.includes(initial as ShopStatus) ? (initial as ShopStatus) : "",
  );
  const [search, setSearch] = useState("");
  const [q, setQ] = useState("");
  const [page, setPage] = useState(1);
  const shops = useAdminShops({ status, q, page });

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold">{t.title}</h1>
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          setQ(search);
          setPage(1);
        }}
      >
        <div className="w-56">
          <SelectField
            label={t.statusFilter}
            value={status}
            onChange={(e) => {
              setStatus(e.target.value as ShopStatus | "");
              setPage(1);
            }}
          >
            <option value="">{messages.market.common.all}</option>
            {statuses.map((s) => (
              <option key={s} value={s}>
                {messages.market.seller.shopStatuses[s]}
              </option>
            ))}
          </SelectField>
        </div>
        <div className="min-w-56 flex-1">
          <TextField
            label={t.search}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            maxLength={100}
          />
        </div>
        <Button type="submit" variant="secondary">
          {messages.market.catalog.apply}
        </Button>
      </form>

      {shops.isPending ? (
        <Spinner label={messages.market.common.loading} />
      ) : shops.isError ? (
        <ErrorState
          title={messages.market.common.loadError}
          onRetry={() => void shops.refetch()}
          retryLabel={messages.market.common.retry}
        />
      ) : shops.data.totalCount === 0 ? (
        <EmptyState title={t.none} />
      ) : (
        <>
          <DataTable
            caption={t.title}
            rows={shops.data.items}
            rowKey={({ shop }) => shop.id}
            rowClassName={({ shop }) => (shop.status === "PendingApproval" ? "bg-warning-50" : undefined)}
            columns={[
              {
                key: "name",
                header: messages.market.seller.name,
                primary: true,
                cell: ({ shop }) => <span className="font-medium">{shop.name}</span>,
              },
              {
                key: "owner",
                header: t.owner,
                cell: ({ ownerEmail }) => <span className="break-all">{ownerEmail ?? "—"}</span>,
              },
              {
                key: "status",
                header: messages.market.orders.status,
                cell: ({ shop }) => (
                  <ShopStatusBadge status={shop.status} labels={messages.market.seller.shopStatuses} />
                ),
              },
              {
                key: "products",
                header: t.activeProducts,
                align: "right",
                cell: ({ activeProducts }) => activeProducts,
              },
              {
                key: "created",
                header: t.created,
                cell: ({ shop }) => formatDateTime(shop.createdAt, messages.market.intlLocale),
              },
              {
                key: "open",
                header: t.open,
                srOnlyHeader: true,
                align: "right",
                cell: ({ shop }) => (
                  <Link
                    href={`/admin/shops/${shop.id}`}
                    className="font-medium text-brand-700 hover:underline"
                  >
                    {t.open}
                  </Link>
                ),
              },
            ]}
          />
          <PagerButtons page={shops.data.page} totalPages={shops.data.totalPages} onChange={setPage} />
        </>
      )}
    </div>
  );
}

type ShopAction = "approve" | "reject" | "suspend" | "reinstate";

/** Which actions the domain allows for each status (the API enforces the same rules and returns 422 otherwise). */
const allowedActions: Record<ShopStatus, ShopAction[]> = {
  PendingApproval: ["approve", "reject", "suspend"],
  Active: ["suspend"],
  Inactive: ["suspend"],
  Suspended: ["reinstate"],
  Rejected: ["suspend"],
};

const needsReason: ShopAction[] = ["reject", "suspend"];

export function AdminShopDetail({ shopId }: { shopId: string }) {
  const { messages } = useI18n();
  const t = messages.admin.shops;
  const c = messages.admin.confirm;
  const queryClient = useQueryClient();
  const detail = useAdminShop(shopId);
  const [pendingAction, setPendingAction] = useState<ShopAction | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const act = useMutation({
    mutationFn: ({ action, reason }: { action: ShopAction; reason: string }) =>
      bffFetch<ShopManagement>(`${adminShopPath(shopId)}/${action}`, {
        method: "POST",
        ...(needsReason.includes(action) ? { body: { reason } } : {}),
      }),
    onSuccess: async () => {
      setPendingAction(null);
      setNotice(t.actionDone);
      await queryClient.invalidateQueries({ queryKey: adminKeys.shop(shopId) });
      await queryClient.invalidateQueries({ queryKey: adminKeys.shops });
      await queryClient.invalidateQueries({ queryKey: adminKeys.overview });
    },
  });

  if (detail.isPending) return <Spinner label={messages.market.common.loading} />;
  if (detail.isError) {
    return detail.error instanceof ApiError && detail.error.kind === "not_found" ? (
      <EmptyState title={t.none} />
    ) : (
      <ErrorState
        title={messages.market.common.loadError}
        onRetry={() => void detail.refetch()}
        retryLabel={messages.market.common.retry}
      />
    );
  }

  const { shop, ownerEmail, activeProducts } = detail.data as AdminShop;
  const dialogText: Record<ShopAction, { title: string; body: string }> = {
    approve: { title: c.approveTitle, body: c.approveBody },
    reject: { title: c.rejectTitle, body: c.rejectBody },
    suspend: { title: c.suspendTitle, body: c.suspendBody },
    reinstate: { title: c.reinstateTitle, body: c.reinstateBody },
  };

  return (
    <div className="flex flex-col gap-6">
      <Link href="/admin/shops" className="text-sm font-medium text-brand-700 hover:underline">
        ← {t.title}
      </Link>
      <Card className="flex flex-col gap-4">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h1 className="text-2xl font-bold">{shop.name}</h1>
          <ShopStatusBadge status={shop.status} labels={messages.market.seller.shopStatuses} />
        </div>
        {notice ? <Alert tone="success">{notice}</Alert> : null}
        {shop.statusReason ? (
          <Alert tone="info">
            <strong>{t.reason}:</strong> {shop.statusReason}
          </Alert>
        ) : null}
        <dl className="grid gap-2 text-sm sm:grid-cols-[10rem_1fr]">
          <dt className="text-ink-600">{t.owner}</dt>
          <dd className="break-all">{ownerEmail ?? "—"}</dd>
          <dt className="text-ink-600">{t.contact}</dt>
          <dd>
            {shop.contactEmail} · {shop.contactPhone}
          </dd>
          <dt className="text-ink-600">{t.address}</dt>
          <dd>
            {shop.addressLine}, {shop.district}, {shop.city} {shop.postalCode ?? ""}
          </dd>
          <dt className="text-ink-600">{t.activeProducts}</dt>
          <dd>{activeProducts}</dd>
          <dt className="text-ink-600">{t.created}</dt>
          <dd>{formatDateTime(shop.createdAt, messages.market.intlLocale)}</dd>
          {shop.description ? (
            <>
              <dt className="text-ink-600">{messages.market.seller.description}</dt>
              <dd className="whitespace-pre-line">{shop.description}</dd>
            </>
          ) : null}
        </dl>
        <div className="flex flex-wrap gap-3">
          {allowedActions[shop.status].map((action) => (
            <Button
              key={action}
              variant={action === "approve" || action === "reinstate" ? "primary" : "secondary"}
              onClick={() => {
                act.reset();
                setNotice(null);
                setPendingAction(action);
              }}
            >
              {t.actions[action]}
            </Button>
          ))}
        </div>
      </Card>

      <CommissionForm shopId={shopId} current={shop.commissionRateBps ?? null} />

      <ConfirmDialog
        open={pendingAction !== null}
        title={pendingAction ? dialogText[pendingAction].title : ""}
        confirmLabel={c.confirm}
        cancelLabel={c.cancel}
        reason={
          pendingAction && needsReason.includes(pendingAction)
            ? { label: c.reasonLabel, requiredMessage: c.reasonRequired }
            : undefined
        }
        busy={act.isPending}
        error={act.isError ? errorMessage(act.error, messages.market) : null}
        onCancel={() => setPendingAction(null)}
        onConfirm={(reason) => pendingAction && act.mutate({ action: pendingAction, reason })}
      >
        {pendingAction ? dialogText[pendingAction].body : null}
      </ConfirmDialog>
    </div>
  );
}

function CommissionForm({ shopId, current }: { shopId: string; current: number | null }) {
  const { messages } = useI18n();
  const t = messages.admin.shops;
  const queryClient = useQueryClient();
  const [value, setValue] = useState(current === null ? "" : String(current));
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const save = useMutation({
    mutationFn: (commissionRateBps: number | null) =>
      bffFetch<ShopManagement>(`${adminShopPath(shopId)}/commission`, {
        method: "PUT",
        body: { commissionRateBps },
      }),
    onSuccess: async () => {
      setSaved(true);
      await queryClient.invalidateQueries({ queryKey: adminKeys.shop(shopId) });
    },
  });

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setSaved(false);
    const trimmed = value.trim();
    const rate = trimmed === "" ? null : Number(trimmed);
    if (rate !== null && (!Number.isInteger(rate) || rate < 0 || rate > 10_000)) {
      setError(messages.market.checkout.errors.required);
      return;
    }

    setError(null);
    save.mutate(rate);
  }

  return (
    <Card>
      <form onSubmit={onSubmit} noValidate className="flex flex-wrap items-end gap-3">
        <div className="min-w-64 flex-1">
          <TextField
            label={t.commission}
            value={value}
            onChange={(e) => setValue(e.target.value)}
            inputMode="numeric"
            hint={t.commissionHint}
            errors={error ? [error] : undefined}
          />
        </div>
        <Button type="submit" variant="secondary" loading={save.isPending}>
          {t.commissionSave}
        </Button>
      </form>
      {saved ? (
        <div className="mt-3">
          <Alert tone="success">{t.commissionSaved}</Alert>
        </div>
      ) : null}
      {save.isError ? (
        <div className="mt-3">
          <Alert tone="error">{errorMessage(save.error, messages.market)}</Alert>
        </div>
      ) : null}
    </Card>
  );
}
