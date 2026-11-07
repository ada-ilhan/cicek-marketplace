import type { Metadata } from "next";
import { AdminOrderList } from "@/features/admin/AdminOrders";
import { AdminShell } from "@/features/admin/AdminShell";
import { adminPermissions } from "@/features/admin/permissions";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return {
    title: (await getRequestI18n()).messages.admin.nav.orders,
    robots: { index: false, follow: false },
  };
}

export default async function Page() {
  return (
    <AdminShell permission={adminPermissions.orders}>
      <AdminOrderList />
    </AdminShell>
  );
}
