"use client";

import { ApiError, type ShopManagement } from "@cicek/api-client";
import { Alert, Button, Card, TextAreaField, TextField } from "@cicek/ui";
import { useQueryClient } from "@tanstack/react-query";
import { useState, type FormEvent } from "react";
import { errorMessage } from "@/features/cart/api";
import { bffFetch } from "@/lib/bff-client";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { sellerKeys, shopPath } from "./api";
import { ShopImageField } from "./ImageManager";
import { profileFromShop, profileSchema, validationMessage, type ProfileField } from "./profile";

/**
 * Owner-only profile form. Sends the version that was loaded, so an edit that raced with another change is rejected
 * (409) instead of silently overwriting it. Editing never changes the approval status.
 */
export function ShopProfileEditor({ shop }: { shop: ShopManagement }) {
  const { messages } = useI18n();
  const t = messages.market;
  const queryClient = useQueryClient();
  const initial = profileFromShop(shop);
  const [errors, setErrors] = useState<Partial<Record<ProfileField, string[]>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [imageUrl, setImageUrl] = useState(shop.imageUrl ?? "");
  const [submitting, setSubmitting] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaved(false);
    const parsed = profileSchema.safeParse(Object.fromEntries(new FormData(event.currentTarget)));
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
      const updated = await bffFetch<ShopManagement>(shopPath(shop.id), {
        method: "PUT",
        body: {
          profile: {
            ...parsed.data,
            description: parsed.data.description || null,
            postalCode: parsed.data.postalCode || null,
            imageUrl: imageUrl || null,
          },
          version: shop.version,
        },
      });
      queryClient.setQueryData(sellerKeys.shop(shop.id), updated);
      await queryClient.invalidateQueries({ queryKey: sellerKeys.shops });
      setSaved(true);
    } catch (error) {
      if (error instanceof ApiError && Object.keys(error.fieldErrors).length > 0) {
        setFormError(t.common.genericError);
      } else {
        setFormError(errorMessage(error, t));
      }
    } finally {
      setSubmitting(false);
    }
  }

  const field = (name: ProfileField) => ({ name, errors: errors[name], defaultValue: initial[name] });
  return (
    <Card>
      <h2 className="mb-4 text-lg font-semibold">{t.seller.editProfile}</h2>
      {saved ? (
        <div className="mb-4">
          <Alert tone="success">{t.seller.profileSaved}</Alert>
        </div>
      ) : null}
      {formError ? (
        <div className="mb-4">
          <Alert tone="error">{formError}</Alert>
        </div>
      ) : null}
      <form key={shop.version} onSubmit={onSubmit} noValidate className="grid gap-4 sm:grid-cols-2">
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
        <ShopImageField shopId={shop.id} value={imageUrl} onChange={setImageUrl} />
        <div className="sm:col-span-2">
          <Button type="submit" loading={submitting}>
            {t.common.save}
          </Button>
        </div>
      </form>
    </Card>
  );
}
