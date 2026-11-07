import { cn } from "./cn";

export type SpinnerProps = { size?: "sm" | "md" } & (
  | {
      /** Hide from assistive technology when the surrounding element already conveys the busy state. */
      decorative: true;
      label?: undefined;
    }
  | {
      decorative?: false;
      /** Accessible label (localized by the caller; the shared UI has no built-in language). */
      label: string;
    }
);

export function Spinner({ size = "md", label, decorative = false }: SpinnerProps) {
  return (
    <span
      role={decorative ? undefined : "status"}
      aria-hidden={decorative || undefined}
      className="inline-flex items-center"
    >
      <span
        className={cn(
          "animate-spin rounded-full border-2 border-current border-r-transparent motion-reduce:animate-none",
          size === "sm" ? "size-4" : "size-6",
        )}
      />
      {decorative ? null : <span className="sr-only">{label}</span>}
    </span>
  );
}
