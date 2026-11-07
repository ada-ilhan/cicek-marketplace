import { useId, type ReactNode, type SelectHTMLAttributes, type TextareaHTMLAttributes } from "react";
import { cn } from "./cn";

interface FieldChromeProps {
  label: string;
  errors?: readonly string[] | undefined;
  hint?: string | undefined;
  required?: boolean | undefined;
  children: (ids: { id: string; describedBy: string | undefined; invalid: boolean }) => ReactNode;
}

/** Label, hint and error wiring shared by form controls (same pattern as TextField). */
function FieldChrome({ label, errors, hint, required, children }: FieldChromeProps) {
  const id = useId();
  const error = errors?.[0];
  const describedBy =
    [hint ? `${id}-hint` : null, error ? `${id}-error` : null].filter(Boolean).join(" ") || undefined;

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium text-ink-900">
        {label}
        {required ? (
          <span aria-hidden="true" className="text-danger-600">
            {" "}
            *
          </span>
        ) : null}
      </label>
      {children({ id, describedBy, invalid: Boolean(error) })}
      {hint ? (
        <p id={`${id}-hint`} className="text-sm text-ink-600">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={`${id}-error`} className="text-sm text-danger-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}

const controlClass = (invalid: boolean) =>
  cn(
    "rounded-lg border bg-white px-3 text-base text-ink-900",
    "focus:outline focus:outline-2 focus:outline-offset-1 focus:outline-brand-600",
    invalid ? "border-danger-600" : "border-ink-200",
  );

export interface SelectFieldProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, "id"> {
  label: string;
  errors?: readonly string[] | undefined;
  hint?: string;
}

export function SelectField({
  label,
  errors,
  hint,
  required,
  className,
  children,
  ...rest
}: SelectFieldProps) {
  return (
    <FieldChrome label={label} errors={errors} hint={hint} required={required}>
      {({ id, describedBy, invalid }) => (
        <select
          id={id}
          required={required}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          className={cn("min-h-11", controlClass(invalid), className)}
          {...rest}
        >
          {children}
        </select>
      )}
    </FieldChrome>
  );
}

export interface TextAreaFieldProps extends Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "id"> {
  label: string;
  errors?: readonly string[] | undefined;
  hint?: string;
}

export function TextAreaField({
  label,
  errors,
  hint,
  required,
  className,
  rows = 4,
  ...rest
}: TextAreaFieldProps) {
  return (
    <FieldChrome label={label} errors={errors} hint={hint} required={required}>
      {({ id, describedBy, invalid }) => (
        <textarea
          id={id}
          rows={rows}
          required={required}
          aria-invalid={invalid || undefined}
          aria-describedby={describedBy}
          className={cn("py-2", controlClass(invalid), className)}
          {...rest}
        />
      )}
    </FieldChrome>
  );
}

type BadgeTone = "neutral" | "info" | "success" | "warning" | "danger";

const badgeTones: Record<BadgeTone, string> = {
  neutral: "bg-ink-100 text-ink-900",
  info: "bg-brand-50 text-brand-900",
  success: "bg-success-50 text-success-800",
  warning: "bg-amber-50 text-amber-900",
  danger: "bg-danger-50 text-danger-800",
};

/** Small status label. Colour is never the only signal: the text carries the meaning. */
export function Badge({ tone = "neutral", children }: { tone?: BadgeTone; children: ReactNode }) {
  return (
    <span
      className={cn(
        "inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold",
        badgeTones[tone],
      )}
    >
      {children}
    </span>
  );
}
