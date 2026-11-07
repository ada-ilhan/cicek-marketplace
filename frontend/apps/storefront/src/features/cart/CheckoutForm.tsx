"use client";

import { ApiError, type CheckoutResult } from "@cicek/api-client";
import { Alert, Button, Card, TextAreaField, TextField } from "@cicek/ui";
import { useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState, type FormEvent } from "react";
import { z } from "zod";
import { localizeFieldErrors } from "@/lib/field-errors";
import { format } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { cartApi, cartKey, errorMessage } from "./api";

/** Mirrors the API's CheckoutRequestValidator; the server validates again (including the delivery window). */
export const checkoutSchema = z.object({
  recipientName: z.string().trim().min(1, "required").max(100, "tooLong"),
  recipientPhone: z
    .string()
    .trim()
    .min(1, "required")
    .max(32, "tooLong")
    .regex(/^\+?[0-9 ()-]{7,}$/, "invalidPhone"),
  addressLine: z.string().trim().min(1, "required").max(200, "tooLong"),
  district: z.string().trim().min(1, "required").max(100, "tooLong"),
  city: z.string().trim().min(1, "required").max(100, "tooLong"),
  postalCode: z.string().trim().max(16, "tooLong"),
  deliveryDate: z.string().regex(/^\d{4}-\d{2}-\d{2}$/, "invalidDate"),
  giftMessage: z.string().trim().max(500, "tooLong"),
});

type FieldName = keyof z.infer<typeof checkoutSchema>;

function todayIso() {
  const now = new Date();
  return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
}

export function CheckoutForm() {
  const t = useI18n().messages.market;
  const queryClient = useQueryClient();
  // One key per checkout attempt: resubmitting (double click, retry after a network error) returns the same orders.
  const [idempotencyKey] = useState(() => crypto.randomUUID());
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<FieldName, string[]>>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<CheckoutResult | null>(null);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFormError(null);
    const values = Object.fromEntries(new FormData(event.currentTarget)) as Record<FieldName, string>;
    const parsed = checkoutSchema.safeParse(values);
    if (!parsed.success) {
      const errors: Partial<Record<FieldName, string[]>> = {};
      for (const issue of parsed.error.issues) {
        const field = issue.path[0] as FieldName;
        const key = issue.message as keyof typeof t.checkout.errors;
        (errors[field] ??= []).push(t.checkout.errors[key] ?? issue.message);
      }
      setFieldErrors(errors);
      return;
    }

    setFieldErrors({});
    setSubmitting(true);
    try {
      const data = parsed.data;
      const checkout = await cartApi.checkout(
        {
          ...data,
          postalCode: data.postalCode || null,
          giftMessage: data.giftMessage || null,
        },
        idempotencyKey,
      );
      setResult(checkout);
      await queryClient.invalidateQueries({ queryKey: cartKey });
      await queryClient.invalidateQueries({ queryKey: ["orders"] });
    } catch (error) {
      if (
        error instanceof ApiError &&
        error.kind === "validation" &&
        Object.keys(error.fieldErrors).length > 0
      ) {
        setFieldErrors(localizeFieldErrors<FieldName>(error.fieldErrors, t.common.invalidValue));
      } else {
        setFormError(errorMessage(error, t));
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (result) {
    return (
      <Card className="flex flex-col gap-4">
        <Alert tone="success" title={t.checkout.successTitle}>
          {format(t.checkout.successDescription, { count: result.orders.length })}
        </Alert>
        <ul className="list-inside list-disc">
          {result.orders.map((order) => (
            <li key={order.orderNumber}>
              <Link
                href={`/account/orders/${order.orderNumber}`}
                className="font-medium text-brand-700 hover:underline"
              >
                {order.orderNumber}
              </Link>{" "}
              — {order.shopName}
            </li>
          ))}
        </ul>
      </Card>
    );
  }

  const field = (name: FieldName) => ({ name, errors: fieldErrors[name] });
  return (
    <Card>
      <form onSubmit={onSubmit} noValidate className="grid gap-5 sm:grid-cols-2">
        <div className="sm:col-span-2">
          <Alert tone="info">{t.checkout.paymentNotice}</Alert>
        </div>
        {formError ? (
          <div className="sm:col-span-2">
            <Alert tone="error">{formError}</Alert>
          </div>
        ) : null}
        <TextField
          label={t.checkout.recipientName}
          required
          autoComplete="name"
          {...field("recipientName")}
        />
        <TextField
          label={t.checkout.recipientPhone}
          required
          type="tel"
          autoComplete="tel"
          {...field("recipientPhone")}
        />
        <div className="sm:col-span-2">
          <TextField
            label={t.checkout.addressLine}
            required
            autoComplete="street-address"
            {...field("addressLine")}
          />
        </div>
        <TextField label={t.checkout.district} required {...field("district")} />
        <TextField label={t.checkout.city} required autoComplete="address-level1" {...field("city")} />
        <TextField label={t.checkout.postalCode} autoComplete="postal-code" {...field("postalCode")} />
        <TextField
          label={t.checkout.deliveryDate}
          required
          type="date"
          min={todayIso()}
          {...field("deliveryDate")}
        />
        <div className="sm:col-span-2">
          <TextAreaField
            label={t.checkout.giftMessage}
            hint={t.checkout.giftMessageHint}
            maxLength={500}
            {...field("giftMessage")}
          />
        </div>
        <div className="sm:col-span-2">
          <Button type="submit" loading={submitting}>
            {t.checkout.submit}
          </Button>
        </div>
      </form>
    </Card>
  );
}
