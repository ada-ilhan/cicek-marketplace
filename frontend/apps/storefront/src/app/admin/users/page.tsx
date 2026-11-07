import type { Metadata } from "next";
import { AdminUsers } from "@/features/admin/AdminUsers";
import { AdminShell } from "@/features/admin/AdminShell";
import { adminPermissions } from "@/features/admin/permissions";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return {
    title: (await getRequestI18n()).messages.admin.nav.users,
    robots: { index: false, follow: false },
  };
}

export default async function Page() {
  return (
    <AdminShell permission={adminPermissions.users}>
      <AdminUsers />
    </AdminShell>
  );
}
