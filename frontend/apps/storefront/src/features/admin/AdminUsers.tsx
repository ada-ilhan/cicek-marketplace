"use client";

import { Badge, Button, EmptyState, ErrorState, SelectField, Spinner, TextField } from "@cicek/ui";
import { useState, type FormEvent } from "react";
import { DataTable } from "@/components/DataTable";
import { PagerButtons } from "@/components/PagerButtons";
import { formatDateTime } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { roleLabel } from "@/lib/labels";
import { useAdminUsers } from "./api";

/** Platform roles (backend Roles). Used only to populate the filter. */
const roles = ["Customer", "FloristOwner", "FloristStaff", "Support", "Finance", "Administrator"];

export function AdminUsers() {
  const { messages } = useI18n();
  const t = messages.admin.users;
  const [search, setSearch] = useState("");
  const [q, setQ] = useState("");
  const [role, setRole] = useState("");
  const [page, setPage] = useState(1);
  const users = useAdminUsers({ q, role, page });

  return (
    <div className="flex flex-col gap-4">
      <h1 className="text-2xl font-bold">{t.title}</h1>
      <p className="text-sm text-ink-600">{t.note}</p>
      <form
        className="flex flex-wrap items-end gap-3"
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          setQ(search);
          setPage(1);
        }}
      >
        <div className="min-w-56 flex-1">
          <TextField
            label={t.search}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            maxLength={100}
          />
        </div>
        <div className="w-56">
          <SelectField
            label={t.role}
            value={role}
            onChange={(e) => {
              setRole(e.target.value);
              setPage(1);
            }}
          >
            <option value="">{messages.market.common.all}</option>
            {roles.map((r) => (
              <option key={r} value={r}>
                {roleLabel(r, messages)}
              </option>
            ))}
          </SelectField>
        </div>
        <Button type="submit" variant="secondary">
          {messages.market.catalog.apply}
        </Button>
      </form>

      {users.isPending ? (
        <Spinner label={messages.market.common.loading} />
      ) : users.isError ? (
        <ErrorState
          title={messages.market.common.loadError}
          onRetry={() => void users.refetch()}
          retryLabel={messages.market.common.retry}
        />
      ) : users.data.totalCount === 0 ? (
        <EmptyState title={t.none} />
      ) : (
        <>
          <DataTable
            caption={t.title}
            rows={users.data.items}
            rowKey={(user) => user.id}
            columns={[
              {
                key: "email",
                header: t.email,
                primary: true,
                cell: (user) => (
                  <span className="break-all">
                    {user.email} {user.isLockedOut ? <Badge tone="danger">{t.locked}</Badge> : null}
                  </span>
                ),
              },
              {
                key: "roles",
                header: t.roles,
                cell: (user) => user.roles.map((role) => roleLabel(role, messages)).join(", ") || "—",
              },
              {
                key: "created",
                header: t.created,
                cell: (user) => formatDateTime(user.createdAt, messages.market.intlLocale),
              },
            ]}
          />
          <PagerButtons page={users.data.page} totalPages={users.data.totalPages} onChange={setPage} />
        </>
      )}
    </div>
  );
}
