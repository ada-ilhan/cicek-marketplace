import type { Metadata } from "next";
import { Suspense } from "react";
import { Card } from "@cicek/ui";
import { AuthForm } from "@/features/auth/AuthForm";
import { getRequestI18n } from "@/lib/i18n/server";

export async function generateMetadata(): Promise<Metadata> {
  return { title: (await getRequestI18n()).messages.auth.loginTitle };
}

export default async function LoginPage() {
  const { messages } = await getRequestI18n();
  return (
    <div className="mx-auto max-w-md">
      <h1 className="mb-6 text-2xl font-bold">{messages.auth.loginTitle}</h1>
      <Card>
        <Suspense>
          <AuthForm mode="login" />
        </Suspense>
      </Card>
    </div>
  );
}
