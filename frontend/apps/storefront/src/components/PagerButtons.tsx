"use client";

import { Button } from "@cicek/ui";
import { format } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";

/** Button-based pagination for client-rendered lists. */
export function PagerButtons({
  page,
  totalPages,
  onChange,
}: {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
}) {
  const t = useI18n().messages.market.common;
  if (totalPages <= 1) return null;
  return (
    <nav aria-label={t.pagination} className="mt-6 flex items-center justify-center gap-3">
      <Button variant="secondary" disabled={page <= 1} onClick={() => onChange(page - 1)}>
        {t.previous}
      </Button>
      <span className="text-sm text-ink-600">{format(t.pageOf, { page, total: totalPages })}</span>
      <Button variant="secondary" disabled={page >= totalPages} onClick={() => onChange(page + 1)}>
        {t.next}
      </Button>
    </nav>
  );
}
