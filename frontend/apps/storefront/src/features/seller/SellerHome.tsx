"use client";

import { ApiError, type ShopManagement } from "@cicek/api-client";
import { Alert, Button, Card, ErrorState, Spinner, TextAreaField, TextField } from "@cicek/ui";
import { useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState, type FormEvent } from "react";
import { ShopStatusBadge } from "@/components/StatusBadge";
import { errorMessage } from "@/features/cart/api";
import { bffFetch } from "@/lib/bff-client";
import { localizeFieldErrors } from "@/lib/field-errors";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { sellerKeys, useMyShops } from "./api";
import { profileSchema, validationMessage, type ProfileField } from "./profile";

export function SellerHome() {
  const t = useI18n().messages.market;
  const shops = useMyShops();

  return (
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-4">
        <h2 className="text-xl font-semibold">{t.seller.myShops}</h2>
        {shops.isPending ? (
          <Spinner label={t.common.loading} />
        ) : shops.isError ? (
          <ErrorState
            title={t.common.loadError}
            onRetry={() => void shops.refetch()}
            retryLabel={t.common.retry}
          />
        ) : shops.data.length === 0 ? null : (
          <ul className="grid gap-4 sm:grid-cols-2">
            {shops.data.map((shop) => (
              <li key={shop.id}>
                <Card className="flex items-center justify-between gap-4">
                  <div>
                    <p className="font-semibold">{shop.name}</p>
                    <ShopStatusBadge status={shop.status} labels={t.seller.shopStatuses} />
                  </div>
                  <Link
                    href={`/seller/shops/${shop.id}`}
                    className="font-medium text-brand-700 hover:underline"
                  >
                    {t.seller.manage}
                  </Link>
                </Card>
              </li>
            ))}
          </ul>
        )}
      </section>
      <ShopApplicationForm />
    </div>
  );
}

function ShopApplicationForm() {
  const { messages } = useI18n();
  const t = messages.market;
  const queryClient = useQueryClient();
  const [errors, setErrors] = useState<Partial<Record<ProfileField, string[]>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [created, setCreated] = useState<ShopManagement | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const parsed = profileSchema.safeParse(Object.fromEntries(new FormData(form)));
    if (!parsed.success) {
      const next: Partial<Record<ProfileField, string[]>> = {};
      for (const issue of parsed.error.issues) {
        const name = issue.path[0] as ProfileField;
        (next[name] ??= []).push(validationMessage(issue, name, messages));
      }
      setErrors(next);
      return;
    }

    setErrors({});
    setFormError(null);
    setSubmitting(true);
    try {
      const shop = await bffFetch<ShopManagement>("/api/bff/v1/shops", {
        method: "POST",
        body: {
          ...parsed.data,
          description: parsed.data.description || null,
          postalCode: parsed.data.postalCode || null,
        },
      });
      setCreated(shop);
      form.reset();
      await queryClient.invalidateQueries({ queryKey: sellerKeys.shops });
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setErrors(localizeFieldErrors<ProfileField>(error.fieldErrors, t.common.invalidValue));
      } else {
        setFormError(errorMessage(error, t));
      }
    } finally {
      setSubmitting(false);
    }
  }

  const field = (name: ProfileField) => ({ name, errors: errors[name] });
  return (
    <Card>
      <h2 className="text-xl font-semibold">{t.seller.applyTitle}</h2>
      <p className="mb-4 text-sm text-ink-600">{t.seller.applyDescription}</p>
      {created ? (
        <div className="mb-4">
          <Alert tone="success">{t.seller.pendingNotice}</Alert>
        </div>
      ) : null}
      {formError ? (
        <div className="mb-4">
          <Alert tone="error">{formError}</Alert>
        </div>
      ) : null}
      <form onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-2">
        <TextField label={t.seller.name} required maxLength={100} {...field("name")} />
        <TextField label={t.seller.contactEmail} type="email" required {...field("contactEmail")} />
        <div className="sm:col-span-2">
          <TextAreaField label={t.seller.description} maxLength={2000} {...field("description")} />
        </div>
        <TextField label={t.seller.contactPhone} type="tel" required {...field("contactPhone")} />
        <TextField label={t.seller.addressLine} required {...field("addressLine")} />
        <TextField label={t.seller.district} required {...field("district")} />
        <TextField label={t.seller.city} required {...field("city")} />
        <TextField label={t.seller.postalCode} {...field("postalCode")} />
        <div className="sm:col-span-2">
          <Button type="submit" loading={submitting}>
            {t.seller.applySubmit}
          </Button>
        </div>
      </form>
    </Card>
  );
}
