import Link from "next/link";
import type { ProductCard as ProductCardData } from "@cicek/api-client";
import { SafeImage } from "@/components/SafeImage";
import { format, formatMoney } from "@/lib/format";
import type { Messages } from "@/lib/i18n/messages";

export function ProductCard({
  product,
  messages,
}: {
  product: ProductCardData;
  messages: Messages["market"];
}) {
  const t = messages.catalog;
  return (
    <li className="flex flex-col overflow-hidden rounded-2xl border border-ink-100 bg-white shadow-sm transition-shadow hover:shadow-md">
      <Link
        href={`/products/${product.id}`}
        className="group flex flex-1 flex-col focus-visible:outline-2 focus-visible:outline-brand-600"
      >
        <div className="relative aspect-square overflow-hidden bg-ink-100">
          {/* The product name below already names the picture, so the image itself is decorative (alt=""). */}
          <SafeImage
            src={product.imageUrl}
            alt=""
            fallbackLabel={t.noImage}
            className="group-hover:scale-105"
          />
          {product.inStock ? null : (
            <span className="absolute left-2 top-2 rounded-full bg-white/95 px-2.5 py-1 text-xs font-semibold text-danger-700 shadow-sm">
              {t.outOfStock}
            </span>
          )}
        </div>
        <div className="flex flex-1 flex-col gap-1 p-4">
          <h3 className="line-clamp-2 font-semibold text-ink-900 group-hover:underline">{product.name}</h3>
          <p className="line-clamp-1 text-sm text-ink-600">{format(t.byShop, { shop: product.shop.name })}</p>
          <p className="mt-auto pt-2 text-lg font-bold text-brand-700">
            {formatMoney(product.price, product.currency, messages.intlLocale)}
          </p>
        </div>
      </Link>
    </li>
  );
}

export function ProductGrid({
  products,
  messages,
}: {
  products: ProductCardData[];
  messages: Messages["market"];
}) {
  return (
    <ul className="grid grid-cols-1 gap-4 min-[420px]:grid-cols-2 sm:gap-6 lg:grid-cols-3">
      {products.map((p) => (
        <ProductCard key={p.id} product={p} messages={messages} />
      ))}
    </ul>
  );
}
