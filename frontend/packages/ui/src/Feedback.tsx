import type { ReactNode } from "react";
import { Button } from "./Button";
import { cn } from "./cn";

type Tone = "info" | "success" | "error";

const tones: Record<Tone, string> = {
  info: "border-brand-200 bg-brand-50 text-brand-900",
  success: "border-success-200 bg-success-50 text-success-800",
  error: "border-danger-200 bg-danger-50 text-danger-800",
};

export interface AlertProps {
  tone?: Tone;
  title?: string;
  children?: ReactNode;
}

/** Inline message. Errors use role="alert" so they are announced immediately. */
export function Alert({ tone = "info", title, children }: AlertProps) {
  return (
    <div
      role={tone === "error" ? "alert" : "status"}
      className={cn("rounded-lg border px-4 py-3 text-sm", tones[tone])}
    >
      {title ? <p className="font-semibold">{title}</p> : null}
      {children ? <div className={title ? "mt-1" : undefined}>{children}</div> : null}
    </div>
  );
}

export function Card({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <section className={cn("rounded-2xl border border-ink-100 bg-white p-6 shadow-sm", className)}>
      {children}
    </section>
  );
}

export interface StateProps {
  title: string;
  description?: string;
  action?: ReactNode;
}

/** Shown when a successful request returns nothing to display. */
export function EmptyState({ title, description, action }: StateProps) {
  return (
    <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed border-ink-200 px-6 py-10 text-center">
      <p className="font-semibold text-ink-900">{title}</p>
      {description ? <p className="max-w-prose text-sm text-ink-600">{description}</p> : null}
      {action}
    </div>
  );
}

export interface ErrorStateProps extends StateProps {
  /** Localized by the caller: the shared UI has no built-in language. */
  retryLabel?: string;
  onRetry?: () => void;
  /** Support reference (trace id) users can quote; contains no technical details. */
  reference?: string | undefined;
  referenceLabel?: string;
}

/** Shown when loading failed; offers a retry when the operation is safe to repeat. */
export function ErrorState({
  title,
  description,
  onRetry,
  retryLabel,
  reference,
  referenceLabel,
}: ErrorStateProps) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center gap-3 rounded-xl border border-danger-200 bg-danger-50 px-6 py-10 text-center"
    >
      <p className="font-semibold text-danger-800">{title}</p>
      {description ? <p className="max-w-prose text-sm text-danger-800">{description}</p> : null}
      {reference && referenceLabel ? (
        <p className="text-xs text-danger-700">
          {referenceLabel}: <code>{reference}</code>
        </p>
      ) : null}
      {onRetry && retryLabel ? (
        <Button variant="secondary" onClick={onRetry}>
          {retryLabel}
        </Button>
      ) : null}
    </div>
  );
}
