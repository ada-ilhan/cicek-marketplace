import Link from "next/link";
import { Alert, EmptyState } from "@cicek/ui";
import { FlowerGlyph } from "@/components/SafeImage";
import { ProductGrid } from "@/features/catalog/ProductCard";
import { ShopGrid } from "@/features/catalog/ShopCard";
import { getRequestI18n } from "@/lib/i18n/server";
import { load, publicApi } from "@/lib/public-api";

export default async function HomePage() {
  const { messages } = await getRequestI18n();
  const t = messages.home;
  const api = publicApi();
  const [shops, products, categories] = await Promise.all([
    load(() => api.GET("/api/v1/shops", { params: { query: { page: 1, pageSize: 6 } } })),
    load(() => api.GET("/api/v1/products", { params: { query: { sort: "Newest", page: 1, pageSize: 8 } } })),
    load(() => api.GET("/api/v1/categories")),
  ]);

  const topCategories = categories.ok ? categories.data.filter((c) => c.parentId === null) : [];
  const sectionLink = "text-sm font-medium text-brand-700 hover:underline";
  const steps = [
    { title: t.step1Title, text: t.step1Text },
    { title: t.step2Title, text: t.step2Text },
    { title: t.step3Title, text: t.step3Text },
  ];

  return (
    <div className="flex flex-col gap-12">
      <section className="relative overflow-hidden rounded-3xl bg-gradient-to-br from-brand-50 via-white to-brand-100 px-6 py-12 shadow-sm sm:px-12 sm:py-16">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-10 -top-10 hidden text-brand-200 sm:block"
        >
          <FlowerGlyph className="size-64 rotate-12" />
        </div>
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -bottom-12 right-24 hidden text-brand-100 lg:block"
        >
          <FlowerGlyph className="size-48 -rotate-12" />
        </div>
        <div className="relative flex max-w-2xl flex-col items-start gap-5">
          <h1 className="text-3xl font-bold tracking-tight text-ink-900 sm:text-5xl">{t.title}</h1>
          <p className="text-lg text-ink-600">{t.subtitle}</p>
          <form
            action="/products"
            method="get"
            role="search"
            className="flex w-full max-w-xl flex-col gap-2 sm:flex-row"
          >
            <label htmlFor="home-search" className="sr-only">
              {t.searchLabel}
            </label>
            <input
              id="home-search"
              name="q"
              type="search"
              maxLength={100}
              placeholder={t.searchPlaceholder}
              className="min-h-12 flex-1 rounded-lg border border-ink-200 bg-white px-4 text-base"
            />
            <button
              type="submit"
              className="min-h-12 rounded-lg bg-brand-600 px-6 font-semibold text-white hover:bg-brand-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-600"
            >
              {t.searchButton}
            </button>
          </form>
          <Link href="/products" className="text-sm font-medium text-brand-700 hover:underline">
            {t.cta} →
          </Link>
        </div>
      </section>

      {topCategories.length > 0 ? (
        <section aria-labelledby="home-categories" className="flex flex-col gap-4">
          <h2 id="home-categories" className="text-2xl font-bold">
            {t.categoriesTitle}
          </h2>
          <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {topCategories.map((parent) => (
              <li key={parent.id} className="rounded-2xl border border-ink-100 bg-white p-5 shadow-sm">
                <Link
                  href={`/products?categoryId=${parent.id}`}
                  className="text-lg font-semibold text-brand-700 hover:underline"
                >
                  {parent.name}
                </Link>
                <ul className="mt-3 flex flex-wrap gap-2">
                  {categories.ok
                    ? categories.data
                        .filter((c) => c.parentId === parent.id)
                        .map((child) => (
                          <li key={child.id}>
                            <Link
                              href={`/products?categoryId=${child.id}`}
                              className="inline-flex min-h-9 items-center rounded-full bg-brand-50 px-3 text-sm text-brand-900 hover:bg-brand-100"
                            >
                              {child.name}
                            </Link>
                          </li>
                        ))
                    : null}
                </ul>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <section aria-labelledby="home-products" className="flex flex-col gap-4">
        <div className="flex items-baseline justify-between">
          <h2 id="home-products" className="text-2xl font-bold">
            {t.latestProducts}
          </h2>
          <Link href="/products" className={sectionLink}>
            {t.viewAll}
          </Link>
        </div>
        {!products.ok ? (
          <Alert tone="error">{messages.market.common.loadError}</Alert>
        ) : products.data.items.length === 0 ? (
          <EmptyState title={t.noProducts} />
        ) : (
          <ProductGrid products={products.data.items} messages={messages.market} />
        )}
      </section>

      <section aria-labelledby="home-shops" className="flex flex-col gap-4">
        <div className="flex items-baseline justify-between">
          <h2 id="home-shops" className="text-2xl font-bold">
            {t.featuredShops}
          </h2>
          <Link href="/shops" className={sectionLink}>
            {t.viewAll}
          </Link>
        </div>
        {!shops.ok ? (
          <Alert tone="error">{messages.market.common.loadError}</Alert>
        ) : shops.data.items.length === 0 ? (
          <EmptyState title={t.noShops} />
        ) : (
          <ShopGrid shops={shops.data.items} messages={messages.market} />
        )}
      </section>

      <section aria-labelledby="home-how" className="flex flex-col gap-4">
        <h2 id="home-how" className="text-2xl font-bold">
          {t.howTitle}
        </h2>
        <ol className="grid gap-4 md:grid-cols-3">
          {steps.map((step, index) => (
            <li
              key={step.title}
              className="flex gap-4 rounded-2xl border border-ink-100 bg-white p-5 shadow-sm"
            >
              <span
                aria-hidden="true"
                className="flex size-9 shrink-0 items-center justify-center rounded-full bg-brand-600 font-bold text-white"
              >
                {index + 1}
              </span>
              <div>
                <h3 className="font-semibold">{step.title}</h3>
                <p className="mt-1 text-sm text-ink-600">{step.text}</p>
              </div>
            </li>
          ))}
        </ol>
      </section>

      <Alert tone="info">{t.paymentNote}</Alert>
    </div>
  );
}
