import type { Metadata } from "next";
import { CartView } from "@/features/cart/CartView";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.market.cart.title, robots: { index: false } };
}

export default async function CartPage() {
  const { messages } = await getRequestI18n();
  return (
    <div className="mx-auto max-w-4xl">
      <h1 className="mb-6 text-2xl font-bold">{messages.market.cart.title}</h1>
      <CartView />
    </div>
  );
}
