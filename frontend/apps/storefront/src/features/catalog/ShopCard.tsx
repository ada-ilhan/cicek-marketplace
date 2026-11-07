import Link from "next/link";
import type { PublicShop } from "@cicek/api-client";
import { SafeImage } from "@/components/SafeImage";
import { format } from "@/lib/format";
import type { Messages } from "@/lib/i18n/messages";

/** Public florist card: uploaded/linked image with a fallback, name, location and a short description. */
export function ShopCard({ shop, messages }: { shop: PublicShop; messages: Messages["market"] }) {
  const t = messages.catalog;
  return (
    <li className="flex flex-col overflow-hidden rounded-2xl border border-ink-100 bg-white shadow-sm transition-shadow hover:shadow-md">
      <Link
        href={`/shops/${shop.slug}`}
        className="group flex flex-1 flex-col focus-visible:outline-2 focus-visible:outline-brand-600"
      >
        <div className="aspect-[16/9] overflow-hidden bg-ink-100">
          <SafeImage src={shop.imageUrl} alt="" fallbackLabel={t.noImage} className="group-hover:scale-105" />
        </div>
        <div className="flex flex-1 flex-col gap-1 p-4">
          <h3 className="line-clamp-1 text-lg font-semibold text-brand-700 group-hover:underline">
            {shop.name}
          </h3>
          <p className="text-sm text-ink-600">
            {format(t.location, { district: shop.district, city: shop.city })}
          </p>
          {shop.description ? (
            <p className="mt-1 line-clamp-2 text-sm text-ink-900">{shop.description}</p>
          ) : null}
        </div>
      </Link>
    </li>
  );
}

export function ShopGrid({ shops, messages }: { shops: PublicShop[]; messages: Messages["market"] }) {
  return (
    <ul className="grid grid-cols-1 gap-4 min-[420px]:grid-cols-2 sm:gap-6 lg:grid-cols-3">
      {shops.map((shop) => (
        <ShopCard key={shop.id} shop={shop} messages={messages} />
      ))}
    </ul>
  );
}
