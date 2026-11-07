import type { Metadata } from "next";
import { EmptyState, ErrorState } from "@cicek/ui";
import { Pagination } from "@/components/Pagination";
import { CatalogFilters } from "@/features/catalog/CatalogFilters";
import { ProductGrid } from "@/features/catalog/ProductCard";
import { filtersToQuery, parseProductFilters } from "@/features/catalog/search-params";
import { format } from "@/lib/format";
import { getRequestI18n } from "@/lib/i18n/server";
import { load, publicApi } from "@/lib/public-api";

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.market.catalog.title };
}

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> };

export default async function ProductsPage({ searchParams }: Props) {
  const { messages } = await getRequestI18n();
  const t = messages.market;
  const filters = parseProductFilters(await searchParams);
  const api = publicApi();

  const [categories, products] = await Promise.all([
    load(() => api.GET("/api/v1/categories")),
    load(() =>
      api.GET("/api/v1/products", {
        params: {
          query: {
            q: filters.q || undefined,
            categoryId: filters.categoryId || undefined,
            minPrice: filters.minPrice ? Number(filters.minPrice) : undefined,
            maxPrice: filters.maxPrice ? Number(filters.maxPrice) : undefined,
            inStock: filters.inStock || undefined,
            sort: filters.sort,
            page: filters.page,
            pageSize: 12,
          },
        },
      }),
    ),
  ]);

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-2xl font-bold">{t.catalog.title}</h1>
      <CatalogFilters
        filters={filters}
        categories={categories.ok ? categories.data : []}
        messages={t.catalog}
      />
      {!products.ok ? (
        <ErrorState
          title={t.common.loadError}
          reference={products.traceId}
          referenceLabel={t.common.reference}
        />
      ) : products.data.totalCount === 0 ? (
        <EmptyState title={t.catalog.noResultsTitle} description={t.catalog.noResultsDescription} />
      ) : (
        <>
          <p className="text-sm text-ink-600" aria-live="polite">
            {format(t.catalog.resultCount, { count: products.data.totalCount })}
          </p>
          <ProductGrid products={products.data.items} messages={t} />
          <Pagination
            page={products.data.page}
            totalPages={products.data.totalPages ?? 0}
            hrefFor={(page) => `/products?${filtersToQuery(filters, { page })}`}
            messages={t.common}
          />
        </>
      )}
    </div>
  );
}
