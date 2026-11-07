import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { EmptyState, ErrorState } from "@cicek/ui";
import { Pagination } from "@/components/Pagination";
import { SafeImage } from "@/components/SafeImage";
import { ProductGrid } from "@/features/catalog/ProductCard";
import { format } from "@/lib/format";
import { getRequestI18n } from "@/lib/i18n/server";
import { load, publicApi } from "@/lib/public-api";

type Props = {
  params: Promise<{ slug: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

function loadShop(slug: string) {
  return load(() => publicApi().GET("/api/v1/shops/{slug}", { params: { path: { slug } } }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const shop = await loadShop((await params).slug);
  return { title: shop.ok ? shop.data.name : (await getRequestI18n()).messages.market.catalog.noShopsTitle };
}

export default async function ShopPage({ params, searchParams }: Props) {
  const { messages } = await getRequestI18n();
  const t = messages.market;
  const { slug } = await params;
  const pageParam = (await searchParams).page;
  const page = Math.max(1, Number.parseInt(typeof pageParam === "string" ? pageParam : "1", 10) || 1);

  const shop = await loadShop(slug);
  if (!shop.ok) {
    if (shop.status === 404) notFound();
    return (
      <ErrorState title={t.common.loadError} reference={shop.traceId} referenceLabel={t.common.reference} />
    );
  }

  const products = await load(() =>
    publicApi().GET("/api/v1/products", { params: { query: { shopId: shop.data.id, page, pageSize: 12 } } }),
  );

  return (
    <div className="flex flex-col gap-6">
      <header className="grid overflow-hidden rounded-2xl bg-white shadow-sm md:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <div className="aspect-[16/9] bg-ink-100 md:aspect-auto md:min-h-48">
          <SafeImage src={shop.data.imageUrl} alt="" fallbackLabel={t.catalog.noImage} priority />
        </div>
        <div className="p-6">
          <h1 className="text-3xl font-bold">{shop.data.name}</h1>
          <p className="text-ink-600">
            {format(t.catalog.location, { district: shop.data.district, city: shop.data.city })}
          </p>
          {shop.data.description ? <p className="mt-3 whitespace-pre-line">{shop.data.description}</p> : null}
        </div>
      </header>
      <h2 className="text-xl font-semibold">{t.catalog.shopProducts}</h2>
      {!products.ok ? (
        <ErrorState
          title={t.common.loadError}
          reference={products.traceId}
          referenceLabel={t.common.reference}
        />
      ) : products.data.totalCount === 0 ? (
        <EmptyState title={t.catalog.noResultsTitle} />
      ) : (
        <>
          <ProductGrid products={products.data.items} messages={t} />
          <Pagination
            page={products.data.page}
            totalPages={products.data.totalPages ?? 0}
            hrefFor={(p) => `/shops/${slug}?page=${p}`}
            messages={t.common}
          />
        </>
      )}
    </div>
  );
}
