import type { AdminOverview, AdminShop, Category, Paged, ShopStatus, UserListItem } from "@cicek/api-client";
import { useQuery } from "@tanstack/react-query";
import { bffFetch } from "@/lib/bff-client";

const base = "/api/bff/v1/admin";

export const adminKeys = {
  overview: ["admin", "overview"] as const,
  shops: ["admin", "shops"] as const,
  shop: (id: string) => ["admin", "shop", id] as const,
  categories: ["admin", "categories"] as const,
  users: ["admin", "users"] as const,
};

export const adminShopPath = (id: string) => `${base}/shops/${encodeURIComponent(id)}`;

export function useAdminOverview() {
  return useQuery({
    queryKey: adminKeys.overview,
    queryFn: () => bffFetch<AdminOverview>(`${base}/overview`),
  });
}

export function useAdminShops(filters: { status: ShopStatus | ""; q: string; page: number }) {
  const query = new URLSearchParams({ page: String(filters.page), pageSize: "15" });
  if (filters.status) query.set("status", filters.status);
  if (filters.q.trim()) query.set("q", filters.q.trim());
  return useQuery({
    queryKey: [...adminKeys.shops, filters],
    queryFn: () => bffFetch<Paged<AdminShop>>(`${base}/shops?${query}`),
  });
}

export function useAdminShop(id: string) {
  return useQuery({ queryKey: adminKeys.shop(id), queryFn: () => bffFetch<AdminShop>(adminShopPath(id)) });
}

export function useAdminCategories() {
  return useQuery({
    queryKey: adminKeys.categories,
    queryFn: () => bffFetch<Category[]>(`${base}/categories`),
  });
}

export function useAdminUsers(filters: { q: string; role: string; page: number }) {
  const query = new URLSearchParams({ page: String(filters.page), pageSize: "15" });
  if (filters.q.trim()) query.set("q", filters.q.trim());
  if (filters.role) query.set("role", filters.role);
  return useQuery({
    queryKey: [...adminKeys.users, filters],
    queryFn: () => bffFetch<Paged<UserListItem>>(`${base}/users?${query}`),
  });
}
