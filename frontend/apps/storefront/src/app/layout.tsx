import type { Metadata } from "next";
import { cookies } from "next/headers";
import Link from "next/link";
import type { ReactNode } from "react";
import { getRequestI18n } from "@/lib/i18n/server";
import { serverConfig } from "@/lib/server-config";
import { readSessionTokens } from "@/lib/session";
import { SiteFooter } from "@/components/SiteFooter";
import { SiteNav } from "@/components/SiteNav";
import { Providers } from "./providers";
import "./globals.css";

export async function generateMetadata(): Promise<Metadata> {
  const { messages } = await getRequestI18n();
  return {
    title: { default: messages.brand, template: `%s · ${messages.brand}` },
    description: messages.home.subtitle,
  };
}

export default async function RootLayout({ children }: { children: ReactNode }) {
  const { locale, messages } = await getRequestI18n();
  // Presence of the HttpOnly refresh cookie only drives navigation; the API authorizes every request.
  const signedIn = Boolean(readSessionTokens(await cookies(), serverConfig().cookieSecure).refreshToken);

  return (
    <html lang={locale}>
      <body className="flex min-h-dvh flex-col font-sans antialiased">
        <a
          href="#main"
          className="sr-only focus:not-sr-only focus:absolute focus:left-4 focus:top-4 focus:rounded focus:bg-white focus:px-3 focus:py-2"
        >
          {messages.nav.skipToContent}
        </a>
        <Providers locale={locale} messages={messages}>
          <header className="relative border-b border-ink-100 bg-white">
            <div className="mx-auto flex max-w-5xl flex-col gap-2 px-4 py-3 md:flex-row md:items-center md:justify-between md:gap-4">
              <Link href="/" className="inline-flex min-h-11 items-center text-xl font-bold text-brand-700">
                {messages.brand}
              </Link>
              <SiteNav signedIn={signedIn} />
            </div>
          </header>
          <main id="main" className="mx-auto w-full max-w-5xl flex-1 px-4 py-8 sm:py-10">
            {children}
          </main>
          <SiteFooter messages={messages} signedIn={signedIn} />
        </Providers>
      </body>
    </html>
  );
}
