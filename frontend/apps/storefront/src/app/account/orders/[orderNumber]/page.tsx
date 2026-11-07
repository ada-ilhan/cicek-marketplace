import type { Metadata } from "next";
import { CustomerOrderDetail } from "@/features/orders/CustomerOrders";

type Props = { params: Promise<{ orderNumber: string }> };

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  return { title: (await params).orderNumber, robots: { index: false } };
}

export default async function OrderPage({ params }: Props) {
  const { orderNumber } = await params;
  return (
    <div className="mx-auto max-w-5xl">
      <CustomerOrderDetail orderNumber={orderNumber} />
    </div>
  );
}
