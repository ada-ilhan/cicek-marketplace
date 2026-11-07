import type { ProductSort } from "@cicek/api-client";

export const productSorts: ProductSort[] = ["Newest", "PriceAscending", "PriceDescending", "Name"];

export interface ProductFilters {
  q: string;
  categoryId: string;
  minPrice: string;
  maxPrice: string;
  inStock: boolean;
  sort: ProductSort;
  page: number;
}

type RawParams = Record<string, string | string[] | undefined>;

const first = (value: string | string[] | undefined) => (Array.isArray(value) ? value[0] : value) ?? "";
const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const price = /^\d{1,7}(\.\d{1,2})?$/;

/** Parses and sanitises catalogue query parameters; invalid values are dropped, never forwarded. */
export function parseProductFilters(params: RawParams): ProductFilters {
  const sort = first(params.sort) as ProductSort;
  const page = Number.parseInt(first(params.page), 10);
  return {
    q: first(params.q).slice(0, 100),
    categoryId: uuid.test(first(params.categoryId)) ? first(params.categoryId) : "",
    minPrice: price.test(first(params.minPrice)) ? first(params.minPrice) : "",
    maxPrice: price.test(first(params.maxPrice)) ? first(params.maxPrice) : "",
    inStock: first(params.inStock) === "true",
    sort: productSorts.includes(sort) ? sort : "Newest",
    page: Number.isFinite(page) && page > 0 ? page : 1,
  };
}

export function filtersToQuery(filters: ProductFilters, overrides: Partial<ProductFilters> = {}): string {
  const merged = { ...filters, ...overrides };
  const query = new URLSearchParams();
  if (merged.q) query.set("q", merged.q);
  if (merged.categoryId) query.set("categoryId", merged.categoryId);
  if (merged.minPrice) query.set("minPrice", merged.minPrice);
  if (merged.maxPrice) query.set("maxPrice", merged.maxPrice);
  if (merged.inStock) query.set("inStock", "true");
  if (merged.sort !== "Newest") query.set("sort", merged.sort);
  if (merged.page > 1) query.set("page", String(merged.page));
  return query.toString();
}
