import type { Metadata } from "next";
import { Suspense } from "react";
import { AdminShopList } from "@/features/admin/AdminShops";
import { AdminShell } from "@/features/admin/AdminShell";
import { adminPermissions } from "@/features/admin/permissions";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return {
    title: (await getRequestI18n()).messages.admin.nav.shops,
    robots: { index: false, follow: false },
  };
}

export default async function Page() {
  return (
    <AdminShell permission={adminPermissions.shops}>
      <Suspense>
        <AdminShopList />
      </Suspense>
    </AdminShell>
  );
}
