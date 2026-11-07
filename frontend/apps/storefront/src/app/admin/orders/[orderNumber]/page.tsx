import type { Metadata } from "next";
import { AdminOrderDetail } from "@/features/admin/AdminOrders";
import { AdminShell } from "@/features/admin/AdminShell";
import { adminPermissions } from "@/features/admin/permissions";
import { getRequestI18n } from "@/lib/i18n/server";

type Props = { params: Promise<{ orderNumber: string }> };

export async function generateMetadata(): Promise<Metadata> {
  return {
    title: (await getRequestI18n()).messages.admin.nav.orders,
    robots: { index: false, follow: false },
  };
}

export default async function Page({ params }: Props) {
  const { orderNumber } = await params;
  return (
    <AdminShell permission={adminPermissions.orders}>
      <AdminOrderDetail orderNumber={orderNumber} />
    </AdminShell>
  );
}
