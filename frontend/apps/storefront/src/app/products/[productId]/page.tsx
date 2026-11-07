import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ErrorState } from "@cicek/ui";
import { SafeImage } from "@/components/SafeImage";
import { AddToCart } from "@/features/cart/AddToCart";
import { format, formatMoney } from "@/lib/format";
import { getRequestI18n } from "@/lib/i18n/server";
import { load, publicApi } from "@/lib/public-api";

type Props = { params: Promise<{ productId: string }> };

function loadProduct(productId: string) {
  return load(() => publicApi().GET("/api/v1/products/{productId}", { params: { path: { productId } } }));
}

export async function generateMetadata({ params }: Props): Promise<Metadata> {
  const product = await loadProduct((await params).productId);
  return {
    title: product.ok ? product.data.name : (await getRequestI18n()).messages.market.catalog.notFoundTitle,
  };
}

export default async function ProductPage({ params }: Props) {
  const { messages } = await getRequestI18n();
  const t = messages.market;
  const product = await loadProduct((await params).productId);

  if (!product.ok) {
    if (product.status === 404 || product.status === 400) notFound();
    return (
      <ErrorState
        title={t.common.loadError}
        reference={product.traceId}
        referenceLabel={t.common.reference}
      />
    );
  }

  const p = product.data;
  const [mainImage, ...moreImages] = p.images;
  return (
    <article className="grid gap-8 md:grid-cols-2">
      <div className="flex flex-col gap-3">
        <div className="aspect-square overflow-hidden rounded-2xl bg-ink-100">
          <SafeImage
            src={mainImage?.url}
            alt={mainImage?.altText || p.name}
            fallbackLabel={t.catalog.noImage}
            priority
          />
        </div>
        {moreImages.length > 0 ? (
          <ul className="grid grid-cols-4 gap-2">
            {moreImages.map((image) => (
              <li key={image.url} className="aspect-square overflow-hidden rounded-lg bg-ink-100">
                <SafeImage src={image.url} alt={image.altText || p.name} fallbackLabel="" />
              </li>
            ))}
          </ul>
        ) : null}
      </div>
      <div className="flex flex-col gap-4">
        <h1 className="text-3xl font-bold">{p.name}</h1>
        <p className="text-sm text-ink-600">
          <Link href={`/shops/${p.shop.slug}`} className="font-medium text-brand-700 hover:underline">
            {format(t.catalog.byShop, { shop: p.shop.name })}
          </Link>
        </p>
        <p className="text-2xl font-bold text-brand-700">{formatMoney(p.price, p.currency, t.intlLocale)}</p>
        <p className={p.inStock ? "font-medium text-success-800" : "font-medium text-danger-700"}>
          {p.inStock ? t.catalog.inStock : t.catalog.outOfStock}
        </p>
        {p.description ? <p className="whitespace-pre-line text-ink-900">{p.description}</p> : null}
        {p.inStock ? <AddToCart productId={p.id} maxQuantity={p.maxOrderQuantity} /> : null}
      </div>
    </article>
  );
}
