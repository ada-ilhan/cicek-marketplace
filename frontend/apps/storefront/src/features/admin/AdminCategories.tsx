"use client";

import type { Category } from "@cicek/api-client";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  ErrorState,
  SelectField,
  Spinner,
  TextField,
} from "@cicek/ui";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { DataTable } from "@/components/DataTable";
import { errorMessage } from "@/features/cart/api";
import { bffFetch } from "@/lib/bff-client";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { adminKeys, useAdminCategories } from "./api";

export function AdminCategories() {
  const { messages } = useI18n();
  const t = messages.admin.categories;
  const categories = useAdminCategories();
  const [editing, setEditing] = useState<Category | null>(null);

  if (categories.isPending) return <Spinner label={messages.market.common.loading} />;
  if (categories.isError) {
    return (
      <ErrorState
        title={messages.market.common.loadError}
        onRetry={() => void categories.refetch()}
        retryLabel={messages.market.common.retry}
      />
    );
  }

  const all = categories.data;
  const parents = all.filter((c) => c.parentId === null);
  const nameOf = (id: string | null | undefined) => all.find((c) => c.id === id)?.name ?? t.topLevel;

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold">{t.title}</h1>
      <p className="text-sm text-ink-600">{t.hint}</p>

      {all.length === 0 ? (
        <EmptyState title={t.none} />
      ) : (
        <DataTable
          caption={t.title}
          rows={all}
          rowKey={(category) => category.id}
          columns={[
            {
              key: "name",
              header: t.name,
              primary: true,
              cell: (category) => (
                <span className={`font-medium ${category.parentId ? "md:pl-4" : ""}`}>{category.name}</span>
              ),
            },
            {
              key: "parent",
              header: t.parent,
              cell: (category) => (category.parentId ? nameOf(category.parentId) : t.topLevel),
            },
            { key: "sort", header: t.sortOrder, align: "right", cell: (category) => category.sortOrder },
            {
              key: "status",
              header: messages.market.orders.status,
              cell: (category) => (
                <Badge tone={category.isActive ? "success" : "neutral"}>
                  {category.isActive ? t.active : t.inactive}
                </Badge>
              ),
            },
            {
              key: "edit",
              header: t.edit,
              srOnlyHeader: true,
              align: "right",
              cell: (category) => (
                <Button variant="ghost" onClick={() => setEditing(category)}>
                  {t.edit}
                </Button>
              ),
            },
          ]}
        />
      )}

      {editing ? (
        <CategoryEditor key={editing.id} category={editing} onDone={() => setEditing(null)} />
      ) : (
        <CategoryCreator parents={parents} />
      )}
    </div>
  );
}

function CategoryCreator({ parents }: { parents: Category[] }) {
  const { messages } = useI18n();
  const t = messages.admin.categories;
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [parentId, setParentId] = useState("");
  const [sortOrder, setSortOrder] = useState("0");
  const [nameError, setNameError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const create = useMutation({
    mutationFn: () =>
      bffFetch<Category>("/api/bff/v1/admin/categories", {
        method: "POST",
        body: { name: name.trim(), parentId: parentId || null, sortOrder: Number(sortOrder) || 0 },
      }),
    onSuccess: async () => {
      setName("");
      setSaved(true);
      await queryClient.invalidateQueries({ queryKey: adminKeys.categories });
    },
  });

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    setSaved(false);
    if (!name.trim()) {
      setNameError(messages.market.checkout.errors.required);
      return;
    }

    setNameError(null);
    create.mutate();
  }

  return (
    <Card>
      <h2 className="mb-3 text-lg font-semibold">{t.add}</h2>
      <form onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-4">
        <div className="sm:col-span-2">
          <TextField
            label={t.name}
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={100}
            required
            errors={nameError ? [nameError] : undefined}
          />
        </div>
        <SelectField label={t.parent} value={parentId} onChange={(e) => setParentId(e.target.value)}>
          <option value="">{t.topLevel}</option>
          {parents.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </SelectField>
        <TextField
          label={t.sortOrder}
          type="number"
          min={0}
          max={10000}
          value={sortOrder}
          onChange={(e) => setSortOrder(e.target.value)}
        />
        <div className="sm:col-span-4">
          <Button type="submit" loading={create.isPending}>
            {t.add}
          </Button>
        </div>
      </form>
      {saved ? (
        <div className="mt-3">
          <Alert tone="success">{t.saved}</Alert>
        </div>
      ) : null}
      {create.isError ? (
        <div className="mt-3">
          <Alert tone="error">{errorMessage(create.error, messages.market)}</Alert>
        </div>
      ) : null}
    </Card>
  );
}

function CategoryEditor({ category, onDone }: { category: Category; onDone: () => void }) {
  const { messages } = useI18n();
  const t = messages.admin.categories;
  const queryClient = useQueryClient();
  const [name, setName] = useState(category.name);
  const [sortOrder, setSortOrder] = useState(String(category.sortOrder));
  const [isActive, setIsActive] = useState(category.isActive);
  const save = useMutation({
    mutationFn: () =>
      bffFetch<Category>(`/api/bff/v1/admin/categories/${category.id}`, {
        method: "PUT",
        body: { name: name.trim(), sortOrder: Number(sortOrder) || 0, isActive },
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: adminKeys.categories });
      onDone();
    },
  });

  return (
    <Card>
      <h2 className="mb-3 text-lg font-semibold">
        {t.edit}: {category.name}
      </h2>
      <form
        onSubmit={(event: FormEvent) => {
          event.preventDefault();
          if (name.trim()) save.mutate();
        }}
        className="grid gap-4 sm:grid-cols-4"
      >
        <div className="sm:col-span-2">
          <TextField
            label={t.name}
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={100}
            required
          />
        </div>
        <TextField
          label={t.sortOrder}
          type="number"
          min={0}
          max={10000}
          value={sortOrder}
          onChange={(e) => setSortOrder(e.target.value)}
        />
        <label className="flex items-center gap-2 self-end pb-2 text-sm font-medium">
          <input
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
            className="size-4"
          />
          {t.active}
        </label>
        <div className="flex gap-3 sm:col-span-4">
          <Button type="submit" loading={save.isPending}>
            {t.save}
          </Button>
          <Button variant="secondary" onClick={onDone}>
            {messages.market.common.cancel}
          </Button>
        </div>
      </form>
      {save.isError ? (
        <div className="mt-3">
          <Alert tone="error">{errorMessage(save.error, messages.market)}</Alert>
        </div>
      ) : null}
    </Card>
  );
}
