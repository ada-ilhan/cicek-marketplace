import { ApiError, type Cart, type CheckoutRequest, type CheckoutResult } from "@cicek/api-client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { bffFetch } from "@/lib/bff-client";
import type { Messages } from "@/lib/i18n/messages";

export const cartKey = ["cart"] as const;

export const cartApi = {
  get: () => bffFetch<Cart>("/api/bff/v1/cart"),
  setItem: (productId: string, quantity: number) =>
    bffFetch<Cart>(`/api/bff/v1/cart/items/${encodeURIComponent(productId)}`, {
      method: "PUT",
      body: { quantity },
    }),
  removeItem: (productId: string) =>
    bffFetch<Cart>(`/api/bff/v1/cart/items/${encodeURIComponent(productId)}`, { method: "DELETE" }),
  checkout: (request: CheckoutRequest, idempotencyKey: string) =>
    bffFetch<CheckoutResult>("/api/bff/v1/checkout", {
      method: "POST",
      body: request,
      headers: { "Idempotency-Key": idempotencyKey },
    }),
};

export function useCart() {
  return useQuery({ queryKey: cartKey, queryFn: cartApi.get });
}

/** Cart mutations return the re-priced cart from the server, which replaces the cached one. */
export function useCartMutation<TArgs>(mutate: (args: TArgs) => Promise<Cart>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: mutate,
    onSuccess: (cart) => queryClient.setQueryData(cartKey, cart),
  });
}

/** Maps API error codes to localized messages; anything unknown gets a generic message (never raw server text). */
export function errorMessage(error: unknown, t: Messages["market"]): string {
  if (!(error instanceof ApiError)) return t.common.genericError;
  switch (error.code) {
    case "cart.insufficient_stock":
    case "checkout.insufficient_stock":
      return t.checkout.errors.insufficientStock;
    case "cart.product_unavailable":
      return t.cart.issues.ProductUnavailable;
    case "checkout.price_changed":
      return t.checkout.errors.priceChanged;
    case "checkout.items_unavailable":
      return t.checkout.errors.itemsUnavailable;
    case "checkout.cart_empty":
      return t.checkout.errors.cartEmpty;
    case "checkout.invalid_delivery_date":
      return t.checkout.errors.invalidDeliveryDate;
    case "conflict.concurrent_update":
      return t.common.conflict;
    case "media.invalid_reference":
      return t.media.errors.invalidReference;
    case "media.unsupported_type":
      return t.media.errors.unsupportedType;
    case "media.too_large":
      return t.media.errors.tooLarge;
    case "media.limit_reached":
      return t.media.errors.limitReached;
    case "media.storage_not_configured":
      return t.media.errors.storageUnavailable;
    default:
      break;
  }

  if (error.kind === "unauthorized") return t.common.signInRequired;
  if (error.kind === "unavailable" || error.kind === "network") return t.common.loadError;
  return t.common.genericError;
}
