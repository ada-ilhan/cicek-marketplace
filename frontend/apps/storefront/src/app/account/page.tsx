import type { Metadata } from "next";
import { AccountView } from "@/features/account/AccountView";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.account.title, robots: { index: false } };
}

export default async function AccountPage() {
  const { messages } = await getRequestI18n();
  return (
    <div className="mx-auto max-w-2xl">
      <h1 className="mb-6 text-2xl font-bold">{messages.account.title}</h1>
      <AccountView />
    </div>
  );
}
