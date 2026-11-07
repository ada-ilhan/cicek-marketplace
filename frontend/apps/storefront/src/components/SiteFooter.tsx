import Link from "next/link";
import type { Messages } from "@/lib/i18n/messages";

/** Site footer: sitemap-style links that exist in the app, plus the copyright line. No invented company details. */
export function SiteFooter({ messages, signedIn }: { messages: Messages; signedIn: boolean }) {
  const link = "text-ink-600 hover:text-brand-700 hover:underline";
  return (
    <footer className="border-t border-ink-100 bg-white">
      <div className="mx-auto grid max-w-5xl gap-8 px-4 py-10 sm:grid-cols-2 lg:grid-cols-4">
        <div className="lg:col-span-1">
          <p className="text-xl font-bold text-brand-700">{messages.brand}</p>
          <p className="mt-2 text-sm text-ink-600">{messages.home.subtitle}</p>
        </div>
        <nav aria-label={messages.footer.explore}>
          <h2 className="mb-3 text-sm font-semibold text-ink-900">{messages.footer.explore}</h2>
          <ul className="flex flex-col gap-2 text-sm">
            <li>
              <Link href="/products" className={link}>
                {messages.market.nav.products}
              </Link>
            </li>
            <li>
              <Link href="/shops" className={link}>
                {messages.market.nav.shops}
              </Link>
            </li>
          </ul>
        </nav>
        <nav aria-label={messages.footer.account}>
          <h2 className="mb-3 text-sm font-semibold text-ink-900">{messages.footer.account}</h2>
          <ul className="flex flex-col gap-2 text-sm">
            {signedIn ? (
              <>
                <li>
                  <Link href="/account" className={link}>
                    {messages.nav.account}
                  </Link>
                </li>
                <li>
                  <Link href="/account/orders" className={link}>
                    {messages.market.nav.orders}
                  </Link>
                </li>
              </>
            ) : (
              <>
                <li>
                  <Link href="/login" className={link}>
                    {messages.nav.signIn}
                  </Link>
                </li>
                <li>
                  <Link href="/register" className={link}>
                    {messages.nav.register}
                  </Link>
                </li>
              </>
            )}
          </ul>
        </nav>
        <nav aria-label={messages.footer.florists}>
          <h2 className="mb-3 text-sm font-semibold text-ink-900">{messages.footer.florists}</h2>
          <ul className="flex flex-col gap-2 text-sm">
            <li>
              <Link href="/seller" className={link}>
                {signedIn ? messages.market.nav.seller : messages.footer.becomeFlorist}
              </Link>
            </li>
          </ul>
        </nav>
      </div>
      <p className="border-t border-ink-100 px-4 py-4 text-center text-sm text-ink-600">
        © {new Date().getUTCFullYear()} {messages.brand}. {messages.footer.rights}
      </p>
    </footer>
  );
}
