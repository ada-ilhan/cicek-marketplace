"use client";

import { ErrorState } from "@cicek/ui";
import { useI18n } from "@/lib/i18n/I18nProvider";

/** Route-level error boundary: shows a generic message and a retry; never renders error details. */
export default function RouteError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const { messages } = useI18n();
  return (
    <ErrorState
      title={messages.errors.title}
      description={messages.errors.description}
      onRetry={reset}
      retryLabel={messages.errors.retry}
      reference={error.digest}
    />
  );
}
