import type { Metadata } from "next";
import { EmptyState, ErrorState, TextField } from "@cicek/ui";
import { Pagination } from "@/components/Pagination";
import { ShopGrid } from "@/features/catalog/ShopCard";
import { getRequestI18n } from "@/lib/i18n/server";
import { load, publicApi } from "@/lib/public-api";

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.market.catalog.shopsTitle };
}

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> };

export default async function ShopsPage({ searchParams }: Props) {
  const { messages } = await getRequestI18n();
  const t = messages.market;
  const params = await searchParams;
  const q = (typeof params.q === "string" ? params.q : "").slice(0, 100);
  const page = Math.max(1, Number.parseInt(typeof params.page === "string" ? params.page : "1", 10) || 1);

  const shops = await load(() =>
    publicApi().GET("/api/v1/shops", { params: { query: { q: q || undefined, page, pageSize: 12 } } }),
  );

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold">{t.catalog.shopsTitle}</h1>
      <form method="get" action="/shops" className="flex items-end gap-3 rounded-2xl bg-white p-4 shadow-sm">
        <div className="flex-1">
          <TextField label={t.catalog.shopSearch} name="q" defaultValue={q} maxLength={100} />
        </div>
        <button
          type="submit"
          className="min-h-11 rounded-lg bg-brand-600 px-5 font-semibold text-white hover:bg-brand-700"
        >
          {t.catalog.apply}
        </button>
      </form>
      {!shops.ok ? (
        <ErrorState
          title={t.common.loadError}
          reference={shops.traceId}
          referenceLabel={t.common.reference}
        />
      ) : shops.data.totalCount === 0 ? (
        <EmptyState title={t.catalog.noShopsTitle} description={t.catalog.noShopsDescription} />
      ) : (
        <>
          <ShopGrid shops={shops.data.items} messages={t} />
          <Pagination
            page={shops.data.page}
            totalPages={shops.data.totalPages ?? 0}
            hrefFor={(p) => `/shops?${new URLSearchParams({ ...(q ? { q } : {}), page: String(p) })}`}
            messages={t.common}
          />
        </>
      )}
    </div>
  );
}
