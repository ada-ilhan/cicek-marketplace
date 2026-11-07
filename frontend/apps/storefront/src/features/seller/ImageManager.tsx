"use client";

import { ApiError, type UploadedImage } from "@cicek/api-client";
import { Alert, Button } from "@cicek/ui";
import { useRef, useState, type ChangeEvent } from "react";
import { SafeImage } from "@/components/SafeImage";
import { bffUpload } from "@/lib/bff-client";
import { format } from "@/lib/format";
import { useI18n } from "@/lib/i18n/I18nProvider";
import type { Messages } from "@/lib/i18n/messages";
import { shopPath } from "./api";

export interface ManagedImage {
  url: string;
  altText: string;
}

export const MAX_UPLOAD_BYTES = 5_000_000;
const ACCEPTED_TYPES = ["image/jpeg", "image/png", "image/webp"];

/** Maps upload failures to localized text (never raw server messages). */
export function uploadErrorMessage(error: unknown, t: Messages["market"]): string {
  const m = t.media.errors;
  if (error instanceof ApiError) {
    switch (error.code) {
      case "media.unsupported_type":
        return m.unsupportedType;
      case "media.too_large":
        return m.tooLarge;
      case "media.empty":
        return m.empty;
      case "media.limit_reached":
        return m.limitReached;
      case "media.invalid_reference":
        return m.invalidReference;
      case "media.storage_not_configured":
        return m.storageUnavailable;
      default:
        break;
    }
    if (error.status === 413) return m.tooLarge;
    if (error.kind === "forbidden") return m.forbidden;
    if (error.kind === "unavailable") return m.storageUnavailable;
  }
  return t.common.genericError;
}

/**
 * Ordered image list for a product: upload (stored by the platform), preview, describe, reorder and remove. The first
 * image is the main one shown on cards. Controlled by the parent form, which sends the list on save.
 */
export function ImageManager({
  shopId,
  images,
  onChange,
  max,
  error,
}: {
  shopId: string;
  images: ManagedImage[];
  onChange: (next: ManagedImage[]) => void;
  max: number;
  error?: string | undefined;
}) {
  const t = useI18n().messages.market;
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [urlDraft, setUrlDraft] = useState("");
  const full = images.length >= max;

  async function onFiles(event: ChangeEvent<HTMLInputElement>) {
    const files = Array.from(event.target.files ?? []);
    event.target.value = "";
    if (files.length === 0) return;
    setMessage(null);

    const next = [...images];
    setBusy(true);
    try {
      for (const file of files) {
        if (next.length >= max) {
          setMessage(format(t.media.errors.tooMany, { max }));
          break;
        }
        // Early feedback only; the server decides from the file's own bytes.
        if (file.size > MAX_UPLOAD_BYTES) {
          setMessage(t.media.errors.tooLarge);
          continue;
        }
        if (file.type && !ACCEPTED_TYPES.includes(file.type)) {
          setMessage(t.media.errors.unsupportedType);
          continue;
        }
        try {
          const uploaded = await bffUpload<UploadedImage>(`${shopPath(shopId)}/images`, file);
          next.push({ url: uploaded.url, altText: "" });
          onChange([...next]);
        } catch (uploadError) {
          setMessage(uploadErrorMessage(uploadError, t));
        }
      }
    } finally {
      setBusy(false);
    }
  }

  function move(index: number, delta: number) {
    const target = index + delta;
    if (target < 0 || target >= images.length) return;
    const next = [...images];
    const [item] = next.splice(index, 1);
    if (item) next.splice(target, 0, item);
    onChange(next);
  }

  function addUrl() {
    const value = urlDraft.trim();
    if (!value) return;
    if (!/^https:\/\/[^\s/$.?#][^\s]*$/i.test(value)) {
      setMessage(t.media.errors.invalidUrl);
      return;
    }
    if (full) {
      setMessage(format(t.media.errors.tooMany, { max }));
      return;
    }
    setMessage(null);
    onChange([...images, { url: value, altText: "" }]);
    setUrlDraft("");
  }

  return (
    <fieldset className="grid gap-3">
      <legend className="mb-1 text-sm font-medium text-ink-900">{t.media.sectionTitle}</legend>
      <p className="text-sm text-ink-600">{format(t.media.fileHint, { max })}</p>

      {images.length === 0 ? (
        <p className="rounded-lg border border-dashed border-ink-200 px-4 py-6 text-center text-sm text-ink-600">
          {t.media.empty}
        </p>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          {images.map((image, index) => (
            <li key={`${image.url}-${index}`} className="flex gap-3 rounded-xl border border-ink-100 p-2">
              <div className="size-24 shrink-0 overflow-hidden rounded-lg bg-ink-100">
                <SafeImage
                  src={image.url}
                  alt={image.altText || format(t.media.imageNumber, { number: index + 1 })}
                  fallbackLabel={t.media.broken}
                />
              </div>
              <div className="flex min-w-0 flex-1 flex-col gap-1.5">
                {index === 0 ? (
                  <span className="w-fit rounded-full bg-brand-50 px-2 py-0.5 text-xs font-semibold text-brand-900">
                    {t.media.primary}
                  </span>
                ) : null}
                <label className="text-xs text-ink-600">
                  <span className="sr-only">
                    {t.media.altLabel} — {format(t.media.imageNumber, { number: index + 1 })}
                  </span>
                  <input
                    type="text"
                    value={image.altText}
                    maxLength={200}
                    placeholder={t.media.altLabel}
                    onChange={(e) =>
                      onChange(images.map((x, i) => (i === index ? { ...x, altText: e.target.value } : x)))
                    }
                    className="min-h-9 w-full rounded-lg border border-ink-200 px-2 text-sm"
                  />
                </label>
                <div className="flex flex-wrap gap-1">
                  <Button
                    variant="ghost"
                    className="min-h-9 px-2"
                    disabled={index === 0}
                    onClick={() => move(index, -1)}
                  >
                    {t.media.moveEarlier}
                  </Button>
                  <Button
                    variant="ghost"
                    className="min-h-9 px-2"
                    disabled={index === images.length - 1}
                    onClick={() => move(index, 1)}
                  >
                    {t.media.moveLater}
                  </Button>
                  <Button
                    variant="ghost"
                    className="min-h-9 px-2 text-danger-700"
                    onClick={() => onChange(images.filter((_, i) => i !== index))}
                  >
                    {t.media.remove}
                  </Button>
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <input
          ref={input}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          multiple
          className="sr-only"
          tabIndex={-1}
          aria-hidden="true"
          data-testid="image-file-input"
          onChange={(e) => void onFiles(e)}
        />
        <Button variant="secondary" loading={busy} disabled={full} onClick={() => input.current?.click()}>
          {busy ? t.media.uploading : t.media.upload}
        </Button>
      </div>

      <div className="flex flex-wrap items-end gap-2">
        <label className="flex min-w-0 flex-1 flex-col gap-1 text-sm font-medium text-ink-900">
          {t.media.addByUrl}
          <input
            type="url"
            inputMode="url"
            value={urlDraft}
            placeholder="https://"
            onChange={(e) => setUrlDraft(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter") {
                e.preventDefault();
                addUrl();
              }
            }}
            className="min-h-11 rounded-lg border border-ink-200 px-3 text-base font-normal"
          />
        </label>
        <Button variant="secondary" disabled={full || !urlDraft.trim()} onClick={addUrl}>
          {t.media.addUrl}
        </Button>
      </div>

      {message ? <Alert tone="error">{message}</Alert> : null}
      {error ? <Alert tone="error">{error}</Alert> : null}
    </fieldset>
  );
}

/** Single branding image for the florist profile: upload (stored by the platform), preview and remove. */
export function ShopImageField({
  shopId,
  value,
  onChange,
}: {
  shopId: string;
  value: string;
  onChange: (next: string) => void;
}) {
  const t = useI18n().messages.market;
  const input = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  async function onFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    setMessage(null);
    if (file.size > MAX_UPLOAD_BYTES) return setMessage(t.media.errors.tooLarge);
    if (file.type && !ACCEPTED_TYPES.includes(file.type)) return setMessage(t.media.errors.unsupportedType);
    setBusy(true);
    try {
      const uploaded = await bffUpload<UploadedImage>(`${shopPath(shopId)}/images`, file);
      onChange(uploaded.url);
    } catch (error) {
      setMessage(uploadErrorMessage(error, t));
    } finally {
      setBusy(false);
    }
  }

  return (
    <fieldset className="grid gap-2 sm:col-span-2">
      <legend className="mb-1 text-sm font-medium text-ink-900">{t.media.shopImage}</legend>
      <p className="text-sm text-ink-600">{t.media.shopImageHint}</p>
      <div className="flex flex-wrap items-center gap-4">
        <div className="size-24 shrink-0 overflow-hidden rounded-xl bg-ink-100">
          <SafeImage src={value || null} alt="" fallbackLabel={t.media.empty} />
        </div>
        <input
          ref={input}
          type="file"
          accept="image/jpeg,image/png,image/webp"
          className="sr-only"
          tabIndex={-1}
          aria-hidden="true"
          data-testid="shop-image-input"
          onChange={(e) => void onFile(e)}
        />
        <Button variant="secondary" loading={busy} onClick={() => input.current?.click()}>
          {busy ? t.media.uploading : t.media.upload}
        </Button>
        {value ? (
          <Button variant="ghost" onClick={() => onChange("")}>
            {t.media.removeShopImage}
          </Button>
        ) : null}
      </div>
      <p className="text-sm text-ink-600">{format(t.media.fileHint, { max: 1 })}</p>
      {message ? <Alert tone="error">{message}</Alert> : null}
    </fieldset>
  );
}
