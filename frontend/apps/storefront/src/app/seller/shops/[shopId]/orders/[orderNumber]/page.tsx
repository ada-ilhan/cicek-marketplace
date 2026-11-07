import type { Metadata } from "next";
import { ShopOrderDetail } from "@/features/seller/ShopOrders";
import { getRequestI18n } from "@/lib/i18n/server";

type Props = { params: Promise<{ shopId: string; orderNumber: string }> };

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.market.seller.title, robots: { index: false } };
}

export default async function Page({ params }: Props) {
  const { shopId, orderNumber } = await params;
  return (
    <div className="mx-auto max-w-5xl">
      <ShopOrderDetail shopId={shopId} orderNumber={orderNumber} />
    </div>
  );
}
