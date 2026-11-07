"use client";

import { ApiError } from "@cicek/api-client";
import { Alert, Button, SelectField } from "@cicek/ui";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { cartApi, errorMessage, useCartMutation } from "./api";

/** Adds a product to the cart. The server checks availability and stock and prices the cart. */
export function AddToCart({ productId, maxQuantity }: { productId: string; maxQuantity: number }) {
  const t = useI18n().messages.market;
  const router = useRouter();
  const [quantity, setQuantity] = useState(1);
  const mutation = useCartMutation(() => cartApi.setItem(productId, quantity));

  function add() {
    mutation.mutate(undefined, {
      onError: (error) => {
        if (error instanceof ApiError && error.kind === "unauthorized") {
          router.push(`/login?next=${encodeURIComponent(`/products/${productId}`)}`);
        }
      },
    });
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-end gap-3">
        <div className="w-28">
          <SelectField
            label={t.catalog.quantity}
            value={quantity}
            onChange={(e) => setQuantity(Number(e.target.value))}
          >
            {Array.from({ length: Math.max(1, maxQuantity) }, (_, i) => i + 1).map((n) => (
              <option key={n} value={n}>
                {n}
              </option>
            ))}
          </SelectField>
        </div>
        <Button onClick={add} loading={mutation.isPending}>
          {t.catalog.addToCart}
        </Button>
      </div>
      {mutation.isSuccess ? (
        <Alert tone="success">
          {t.catalog.added}{" "}
          <Link href="/cart" className="font-semibold underline">
            {t.catalog.viewCart}
          </Link>
        </Alert>
      ) : null}
      {mutation.isError && !(mutation.error instanceof ApiError && mutation.error.kind === "unauthorized") ? (
        <Alert tone="error">{errorMessage(mutation.error, t)}</Alert>
      ) : null}
    </div>
  );
}
