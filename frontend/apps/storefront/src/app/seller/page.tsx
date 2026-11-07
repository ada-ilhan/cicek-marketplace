import type { Metadata } from "next";
import { SellerHome } from "@/features/seller/SellerHome";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.market.seller.title, robots: { index: false } };
}

export default async function Page() {
  const { messages } = await getRequestI18n();
  return (
    <div className="mx-auto max-w-5xl">
      <>
        <h1 className="mb-2 text-2xl font-bold">{messages.market.seller.title}</h1>
        <p className="mb-6 text-ink-600">{messages.market.seller.intro}</p>
        <SellerHome />
      </>
    </div>
  );
}
