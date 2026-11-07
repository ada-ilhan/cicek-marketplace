import type { Category, Paged, ShopManagement, ShopProduct } from "@cicek/api-client";
import { useQuery } from "@tanstack/react-query";
import { bffFetch } from "@/lib/bff-client";

export const sellerKeys = {
  shops: ["seller", "shops"] as const,
  shop: (shopId: string) => ["seller", "shop", shopId] as const,
  products: (shopId: string, page: number, q = "", status = "") =>
    ["seller", "products", shopId, page, q, status] as const,
  product: (shopId: string, productId: string) => ["seller", "product", shopId, productId] as const,
};

export const shopPath = (shopId: string) => `/api/bff/v1/my/shops/${encodeURIComponent(shopId)}`;

export function useMyShops() {
  return useQuery({
    queryKey: sellerKeys.shops,
    queryFn: () => bffFetch<ShopManagement[]>("/api/bff/v1/my/shops"),
  });
}

export function useMyShop(shopId: string) {
  return useQuery({
    queryKey: sellerKeys.shop(shopId),
    queryFn: () => bffFetch<ShopManagement>(shopPath(shopId)),
  });
}

export function useShopProducts(
  shopId: string,
  page: number,
  filter: { q?: string; status?: string; pageSize?: number; outOfStock?: boolean } = {},
) {
  const { q = "", status = "", pageSize = 20, outOfStock = false } = filter;
  return useQuery({
    queryKey: [...sellerKeys.products(shopId, page, q, status), pageSize, outOfStock],
    queryFn: () => {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (q.trim()) params.set("q", q.trim());
      if (status) params.set("status", status);
      if (outOfStock) params.set("outOfStock", "true");
      return bffFetch<Paged<ShopProduct>>(`${shopPath(shopId)}/products?${params}`);
    },
  });
}

export function useCategories() {
  return useQuery({
    queryKey: ["categories"],
    queryFn: () => bffFetch<Category[]>("/api/bff/v1/categories"),
    staleTime: 5 * 60_000,
  });
}
