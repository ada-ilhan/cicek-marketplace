"use client";

import type { CartLine } from "@cicek/api-client";
import { Alert, Button, Card, EmptyState, ErrorState, Spinner } from "@cicek/ui";
import Link from "next/link";
import { format, formatMoney } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { cartApi, errorMessage, useCart, useCartMutation } from "./api";

/** Cart grouped by shop (each group becomes one order). All amounts come from the server. */
export function CartView() {
  const t = useI18n().messages.market;
  const cart = useCart();
  const setItem = useCartMutation(({ productId, quantity }: { productId: string; quantity: number }) =>
    cartApi.setItem(productId, quantity),
  );
  const removeItem = useCartMutation((productId: string) => cartApi.removeItem(productId));

  if (cart.isPending) {
    return (
      <Card className="flex justify-center py-16">
        <Spinner label={t.common.loading} />
      </Card>
    );
  }

  if (cart.isError) {
    return (
      <ErrorState
        title={t.common.loadError}
        onRetry={() => void cart.refetch()}
        retryLabel={t.common.retry}
      />
    );
  }

  if (cart.data.shops.length === 0) {
    return (
      <EmptyState
        title={t.cart.emptyTitle}
        description={t.cart.emptyDescription}
        action={
          <Link href="/products" className="font-medium text-brand-700 hover:underline">
            {t.cart.browse}
          </Link>
        }
      />
    );
  }

  const currency = cart.data.currency;
  const money = (amount: number) => formatMoney(amount, currency, t.intlLocale);
  const mutationError = setItem.error ?? removeItem.error;
  const busy = setItem.isPending || removeItem.isPending;

  return (
    <div className="flex flex-col gap-6">
      {mutationError ? <Alert tone="error">{errorMessage(mutationError, t)}</Alert> : null}
      {cart.data.canCheckout ? null : <Alert tone="error">{t.cart.issuesAlert}</Alert>}
      {cart.data.shops.length > 1 ? <Alert tone="info">{t.cart.splitNotice}</Alert> : null}

      {cart.data.shops.map((group) => (
        <Card key={group.shopId} className="flex flex-col gap-4">
          <h2 className="text-lg font-semibold">{group.shopName}</h2>
          <ul className="divide-y divide-ink-100">
            {group.lines.map((line) => (
              <CartLineRow
                key={line.productId}
                line={line}
                money={money}
                busy={busy}
                onQuantity={(quantity) => setItem.mutate({ productId: line.productId, quantity })}
                onRemove={() => removeItem.mutate(line.productId)}
              />
            ))}
          </ul>
          <dl className="ml-auto grid w-full max-w-xs grid-cols-2 gap-1 text-sm">
            <dt className="text-ink-600">{t.cart.subtotal}</dt>
            <dd className="text-right">{money(group.subtotal)}</dd>
            <dt className="text-ink-600">{t.cart.deliveryFee}</dt>
            <dd className="text-right">{money(group.deliveryFee)}</dd>
            <dt className="font-semibold">{t.cart.shopTotal}</dt>
            <dd className="text-right font-semibold">{money(group.total)}</dd>
          </dl>
        </Card>
      ))}

      <Card className="flex flex-wrap items-center justify-between gap-4">
        <p className="text-lg">
          {t.cart.grandTotal}: <strong>{money(cart.data.total)}</strong>
        </p>
        {cart.data.canCheckout ? (
          <Link
            href="/checkout"
            className="inline-flex min-h-11 items-center rounded-lg bg-brand-600 px-5 font-semibold text-white hover:bg-brand-700"
          >
            {t.cart.checkout}
          </Link>
        ) : (
          <Button disabled>{t.cart.checkout}</Button>
        )}
      </Card>
    </div>
  );
}

function CartLineRow({
  line,
  money,
  busy,
  onQuantity,
  onRemove,
}: {
  line: CartLine;
  money: (amount: number) => string;
  busy: boolean;
  onQuantity: (quantity: number) => void;
  onRemove: () => void;
}) {
  const t = useI18n().messages.market;
  const options = Array.from({ length: Math.max(line.quantity, line.availableQuantity, 1) }, (_, i) => i + 1);
  return (
    <li className="flex flex-wrap items-center gap-4 py-3">
      <div className="min-w-0 flex-1">
        <Link href={`/products/${line.productId}`} className="font-medium hover:underline">
          {line.productName}
        </Link>
        <p className="text-sm text-ink-600">
          {t.cart.unitPrice}: {money(line.unitPrice)}
        </p>
        {line.issue !== "None" ? (
          <p className="text-sm font-medium text-danger-700">
            {format(t.cart.issues[line.issue], { available: line.availableQuantity })}
          </p>
        ) : null}
      </div>
      <label className="flex items-center gap-2 text-sm">
        <span>{t.cart.quantity}</span>
        <select
          className="min-h-11 rounded-lg border border-ink-200 bg-white px-2"
          value={line.quantity}
          disabled={busy}
          onChange={(e) => onQuantity(Number(e.target.value))}
        >
          {options.map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </select>
      </label>
      <p className="w-28 text-right font-semibold">{money(line.lineTotal)}</p>
      <Button variant="ghost" onClick={onRemove} disabled={busy}>
        {t.cart.remove}
      </Button>
    </li>
  );
}
