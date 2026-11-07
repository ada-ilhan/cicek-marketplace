import Link from "next/link";
import { EmptyState } from "@cicek/ui";
import { getRequestI18n } from "@/lib/i18n/server";

export default async function NotFound() {
  const { messages } = await getRequestI18n();
  return (
    <EmptyState
      title={messages.errors.notFoundTitle}
      description={messages.errors.notFoundDescription}
      action={
        <Link href="/" className="font-medium text-brand-700 hover:underline">
          {messages.errors.backHome}
        </Link>
      }
    />
  );
}
