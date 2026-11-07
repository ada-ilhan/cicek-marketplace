import { Badge } from "@cicek/ui";
import type { OrderStatus, ProductStatus, ShopStatus } from "@cicek/api-client";

type Tone = "neutral" | "info" | "success" | "warning" | "danger";

const orderTones: Record<OrderStatus, Tone> = {
  PendingPayment: "warning",
  Paid: "info",
  Accepted: "info",
  Preparing: "info",
  ReadyForDelivery: "info",
  Delivered: "success",
  Cancelled: "neutral",
  Rejected: "danger",
  Expired: "neutral",
};

const shopTones: Record<ShopStatus, Tone> = {
  PendingApproval: "warning",
  Active: "success",
  Inactive: "neutral",
  Suspended: "danger",
  Rejected: "danger",
};

export function OrderStatusBadge({
  status,
  labels,
}: {
  status: OrderStatus;
  labels: Record<OrderStatus, string>;
}) {
  return <Badge tone={orderTones[status]}>{labels[status]}</Badge>;
}

export function ShopStatusBadge({
  status,
  labels,
}: {
  status: ShopStatus;
  labels: Record<ShopStatus, string>;
}) {
  return <Badge tone={shopTones[status]}>{labels[status]}</Badge>;
}

export function ProductStatusBadge({
  status,
  labels,
}: {
  status: ProductStatus;
  labels: Record<ProductStatus, string>;
}) {
  return <Badge tone={status === "Active" ? "success" : "neutral"}>{labels[status]}</Badge>;
}
