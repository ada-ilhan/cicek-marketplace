import { useId, type InputHTMLAttributes } from "react";
import { cn } from "./cn";

export interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id"> {
  label: string;
  /** Validation messages (client or server). The first is shown and linked via aria-describedby. */
  errors?: readonly string[] | undefined;
  hint?: string;
}

/** Labelled input with accessible error and hint wiring. */
export function TextField({ label, errors, hint, className, required, ...rest }: TextFieldProps) {
  const id = useId();
  const errorId = `${id}-error`;
  const hintId = `${id}-hint`;
  const error = errors?.[0];
  const describedBy = [hint ? hintId : null, error ? errorId : null].filter(Boolean).join(" ") || undefined;

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
      <input
        id={id}
        required={required}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy}
        className={cn(
          "min-h-11 rounded-lg border bg-white px-3 text-base text-ink-900 placeholder:text-ink-400",
          "focus:outline focus:outline-2 focus:outline-offset-1 focus:outline-brand-600",
          error ? "border-danger-600" : "border-ink-200",
          className,
        )}
        {...rest}
      />
      {hint ? (
        <p id={hintId} className="text-sm text-ink-600">
          {hint}
        </p>
      ) : null}
      {error ? (
        <p id={errorId} className="text-sm text-danger-700">
          {error}
        </p>
      ) : null}
    </div>
  );
}
