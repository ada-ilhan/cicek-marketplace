"use client";

import { ApiError } from "@cicek/api-client";
import { Button, Card, EmptyState, ErrorState, Spinner } from "@cicek/ui";
import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { authApi, useCurrentUser } from "@/features/auth/api";
import { useI18n } from "@/lib/i18n/I18nProvider";
import { permissionLabel, roleLabel } from "@/lib/labels";

/** Account overview with explicit loading, error, empty and success states. */
export function AccountView() {
  const { messages } = useI18n();
  const t = messages.account;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { data: user, error, isPending, refetch } = useCurrentUser();
  const [signingOut, setSigningOut] = useState(false);

  const unauthorized = error instanceof ApiError && error.kind === "unauthorized";
  useEffect(() => {
    if (unauthorized) router.replace("/login?next=/account");
  }, [unauthorized, router]);

  async function signOut() {
    setSigningOut(true);
    try {
      await authApi.logout();
    } finally {
      queryClient.clear();
      router.replace("/");
      router.refresh();
    }
  }

  if (isPending || unauthorized) {
    return (
      <Card className="flex justify-center py-16">
        <Spinner label={t.loading} />
      </Card>
    );
  }

  if (error) {
    return (
      <ErrorState
        title={t.loadError}
        onRetry={() => void refetch()}
        retryLabel={t.retry}
        reference={error instanceof ApiError ? error.traceId : undefined}
        referenceLabel={t.reference}
      />
    );
  }

  return (
    <Card className="flex flex-col gap-6">
      <dl className="grid gap-4 sm:grid-cols-[10rem_1fr]">
        <dt className="text-sm font-medium text-ink-600">{t.email}</dt>
        <dd className="break-all text-ink-900">{user.email}</dd>
        <dt className="text-sm font-medium text-ink-600">{t.roles}</dt>
        <dd className="text-ink-900">{user.roles.map((role) => roleLabel(role, messages)).join(", ")}</dd>
      </dl>
      <div className="flex flex-col gap-3">
        <h2 className="text-sm font-medium text-ink-600">{t.permissions}</h2>
        {user.permissions.length === 0 ? (
          <EmptyState title={t.noPermissionsTitle} description={t.noPermissionsDescription} />
        ) : (
          <ul className="list-inside list-disc text-ink-900">
            {user.permissions.map((permission) => (
              <li key={permission}>{permissionLabel(permission, messages)}</li>
            ))}
          </ul>
        )}
      </div>
      <div>
        <Button variant="secondary" onClick={() => void signOut()} loading={signingOut}>
          {t.signOut}
        </Button>
      </div>
    </Card>
  );
}
