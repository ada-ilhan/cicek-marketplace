import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export * from "./errors";
export type { components, paths };

type Schemas = components["schemas"];

/** DTOs generated from the API's OpenAPI document (backend/openapi/cicek-api.json). */
export type AuthSession = Schemas["AuthSessionDto"];
export type CurrentUser = Schemas["CurrentUserDto"];
export type LoginRequest = Schemas["LoginCommand"];
export type RegisterRequest = Schemas["RegisterCustomerCommand"];
export type SystemStatus = Schemas["SystemStatusDto"];

export type Category = Schemas["CategoryDto"];
export type PublicShop = Schemas["PublicShopDto"];
export type ProductCard = Schemas["ProductCardDto"];
export type ProductDetail = Schemas["ProductDetailDto"];
export type ProductSort = NonNullable<Schemas["ProductSort"]>;
export type ProductStatus = Schemas["ProductStatus"];
export type ShopProduct = Schemas["ShopProductDto"];
export type ProductUpsertRequest = Schemas["ProductUpsertRequest"];
export type UploadedImage = Schemas["UploadedImageDto"];

export type ShopManagement = Schemas["ShopManagementDto"];
export type ShopProfileRequest = Schemas["ShopProfileRequest"];
export type ShopStatus = Schemas["ShopStatus"];

export type Cart = Schemas["CartDto"];
export type CartLine = Schemas["CartLineDto"];
export type CartLineIssue = Schemas["CartLineIssue"];
export type CheckoutRequest = Schemas["CheckoutRequest"];
export type CheckoutResult = Schemas["CheckoutResultDto"];

export type OrderSummary = Schemas["OrderSummaryDto"];
export type OrderDetail = Schemas["OrderDetailDto"];
export type OrderStatus = Schemas["OrderStatus"];

/** Paged list envelope used by every collection endpoint. */
export interface Paged<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/**
 * Typed client for the ASP.NET Core API. Server-side only (used by the BFF and server components):
 * browsers never call the API directly and never see access tokens.
 */
export function createApiClient(options: { baseUrl: string; fetch?: typeof globalThis.fetch }) {
  return createClient<paths>({
    baseUrl: options.baseUrl,
    ...(options.fetch ? { fetch: options.fetch } : {}),
  });
}

export type ApiClient = ReturnType<typeof createApiClient>;

export type AdminOverview = Schemas["AdminOverviewDto"];
export type AdminShop = Schemas["AdminShopDto"];
export type UserListItem = Schemas["UserListItemDto"];
