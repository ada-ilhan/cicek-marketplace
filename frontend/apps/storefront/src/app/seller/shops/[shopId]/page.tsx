import type { Metadata } from "next";
import { ShopDashboard } from "@/features/seller/ShopDashboard";
import { getRequestI18n } from "@/lib/i18n/server";

type Props = { params: Promise<{ shopId: string }> };

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.market.seller.title, robots: { index: false } };
}

export default async function Page({ params }: Props) {
  const { shopId } = await params;
  return (
    <div className="mx-auto max-w-5xl">
      <ShopDashboard shopId={shopId} />
    </div>
  );
}
