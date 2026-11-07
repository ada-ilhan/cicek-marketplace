import type { Metadata } from "next";
import { CustomerOrderList } from "@/features/orders/CustomerOrders";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.market.orders.title, robots: { index: false } };
}

export default async function OrdersPage() {
  const { messages } = await getRequestI18n();
  return (
    <div className="mx-auto max-w-5xl">
      <h1 className="mb-6 text-2xl font-bold">{messages.market.orders.title}</h1>
      <CustomerOrderList />
    </div>
  );
}
