import Link from "next/link";
import { format } from "@/lib/format";
import type { Messages } from "@/lib/i18n/messages";

/** Link-based pagination for server-rendered lists (works without JavaScript). */
export function Pagination({
  page,
  totalPages,
  hrefFor,
  messages,
}: {
  page: number;
  totalPages: number;
  hrefFor: (page: number) => string;
  messages: Messages["market"]["common"];
}) {
  if (totalPages <= 1) return null;
  const linkClass = "rounded-lg border border-ink-200 bg-white px-3 py-2 text-sm font-medium hover:bg-ink-50";
  return (
    <nav aria-label={messages.pagination} className="mt-8 flex items-center justify-center gap-3">
      {page > 1 ? (
        <Link href={hrefFor(page - 1)} className={linkClass} rel="prev">
          {messages.previous}
        </Link>
      ) : null}
      <span className="text-sm text-ink-600" aria-current="page">
        {format(messages.pageOf, { page, total: totalPages })}
      </span>
      {page < totalPages ? (
        <Link href={hrefFor(page + 1)} className={linkClass} rel="next">
          {messages.next}
        </Link>
      ) : null}
    </nav>
  );
}
