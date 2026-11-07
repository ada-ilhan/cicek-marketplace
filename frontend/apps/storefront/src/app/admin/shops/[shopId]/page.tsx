import type { Metadata } from "next";
import { AdminShopDetail } from "@/features/admin/AdminShops";
import { AdminShell } from "@/features/admin/AdminShell";
import { adminPermissions } from "@/features/admin/permissions";
import { getRequestI18n } from "@/lib/i18n/server";

type Props = { params: Promise<{ shopId: string }> };

export async function generateMetadata(): Promise<Metadata> {
  return {
    title: (await getRequestI18n()).messages.admin.nav.shops,
    robots: { index: false, follow: false },
  };
}

export default async function Page({ params }: Props) {
  const { shopId } = await params;
  return (
    <AdminShell permission={adminPermissions.shops}>
      <AdminShopDetail shopId={shopId} />
    </AdminShell>
  );
}
