import type { Metadata } from "next";
import { CheckoutForm } from "@/features/cart/CheckoutForm";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.market.checkout.title, robots: { index: false } };
}

export default async function CheckoutPage() {
  const { messages } = await getRequestI18n();
  return (
    <div className="mx-auto max-w-3xl">
      <h1 className="mb-6 text-2xl font-bold">{messages.market.checkout.title}</h1>
      <CheckoutForm />
    </div>
  );
}
