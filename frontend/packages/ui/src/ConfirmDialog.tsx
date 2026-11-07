"use client";

import { useEffect, useId, useRef, useState, type KeyboardEvent, type ReactNode } from "react";
import { Alert } from "./Feedback";
import { Button } from "./Button";
import { TextAreaField } from "./Fields";

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  children?: ReactNode;
  confirmLabel: string;
  cancelLabel: string;
  /** When set, a reason field is shown and must be filled before confirming. */
  reason?: { label: string; requiredMessage: string };
  /** Visual emphasis of the confirm button (destructive actions use "secondary" to avoid inviting a click). */
  confirmVariant?: "primary" | "secondary";
  busy?: boolean;
  /** Server error to show inside the dialog (e.g. a conflict). */
  error?: string | null;
  onConfirm: (reason: string) => void;
  onCancel: () => void;
}

const FOCUSABLE = 'button:not([disabled]), textarea, input, select, [href], [tabindex]:not([tabindex="-1"])';

/**
 * Modal confirmation for important actions. Accessible: role="dialog" with aria-modal and a labelled title, focus moves
 * into the dialog and is trapped while it is open, Escape cancels, and focus returns to the trigger afterwards.
 */
export function ConfirmDialog({
  open,
  title,
  children,
  confirmLabel,
  cancelLabel,
  reason,
  confirmVariant = "primary",
  busy = false,
  error,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const titleId = useId();
  const panelRef = useRef<HTMLDivElement>(null);
  const [reasonText, setReasonText] = useState("");
  const [reasonError, setReasonError] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    const previous = document.activeElement as HTMLElement | null;
    setReasonText("");
    setReasonError(null);
    panelRef.current?.querySelector<HTMLElement>("textarea, button")?.focus();
    return () => previous?.focus();
  }, [open]);

  if (!open) return null;

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (event.key === "Escape" && !busy) {
      event.stopPropagation();
      onCancel();
      return;
    }

    if (event.key !== "Tab") return;
    const items = [...(panelRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? [])];
    const first = items[0];
    const last = items[items.length - 1];
    if (!first || !last) return;
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  function submit() {
    if (reason && !reasonText.trim()) {
      setReasonError(reason.requiredMessage);
      return;
    }
    onConfirm(reasonText.trim());
  }

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-ink-900/50 p-4"
      onKeyDown={onKeyDown}
    >
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        className="flex w-full max-w-md flex-col gap-4 rounded-2xl bg-white p-6 shadow-xl"
      >
        <h2 id={titleId} className="text-lg font-semibold">
          {title}
        </h2>
        {children ? <div className="text-sm text-ink-600">{children}</div> : null}
        {reason ? (
          <TextAreaField
            label={reason.label}
            value={reasonText}
            maxLength={500}
            rows={3}
            errors={reasonError ? [reasonError] : undefined}
            onChange={(e) => setReasonText(e.target.value)}
          />
        ) : null}
        {error ? <Alert tone="error">{error}</Alert> : null}
        <div className="flex justify-end gap-3">
          <Button variant="secondary" onClick={onCancel} disabled={busy}>
            {cancelLabel}
          </Button>
          <Button variant={confirmVariant} onClick={submit} loading={busy}>
            {confirmLabel}
          </Button>
        </div>
      </div>
    </div>
  );
}
