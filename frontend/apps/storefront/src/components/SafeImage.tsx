"use client";

import { cn } from "@cicek/ui";
import { useState } from "react";

/**
 * Image with a graceful fallback: when the source is missing or fails to load (dead link, blocked host, deleted file)
 * a neutral placeholder is shown instead of a broken-image icon. The box keeps its size, so layouts never jump.
 * `alt` should describe the picture when it carries meaning; pass "" when the surrounding text already names it.
 */
export function SafeImage({
  src,
  alt,
  fallbackLabel,
  className,
  priority = false,
}: {
  src: string | null | undefined;
  alt: string;
  /** Text shown in the placeholder (localized by the caller). */
  fallbackLabel: string;
  className?: string;
  priority?: boolean;
}) {
  const [failedSrc, setFailedSrc] = useState<string | null>(null);
  const failed = !src || failedSrc === src;

  if (failed) {
    return (
      <div
        role={alt ? "img" : undefined}
        aria-label={alt || undefined}
        className={cn(
          "flex size-full flex-col items-center justify-center gap-2 bg-brand-50 text-ink-600",
          className,
        )}
      >
        <FlowerGlyph className="size-10 text-brand-300" />
        <span className="px-2 text-center text-xs">{fallbackLabel}</span>
      </div>
    );
  }

  return (
    // Uploaded images are same-origin (/media/{id}); external HTTPS hosts are allow-listed by the API. A plain img keeps
    // the BFF free of an image optimizer for now (see docs/UI_UX_AUDIT.md).
    // eslint-disable-next-line @next/next/no-img-element
    <img
      src={src}
      alt={alt}
      loading={priority ? "eager" : "lazy"}
      decoding="async"
      onError={() => setFailedSrc(src)}
      // An image that already failed before hydration never fires onError on the client: detect it on mount.
      ref={(element) => {
        if (element?.complete && element.naturalWidth === 0) setFailedSrc(src);
      }}
      className={cn("size-full object-cover transition-transform duration-300", className)}
    />
  );
}

/** Original decorative flower mark (inline SVG, no external asset). */
export function FlowerGlyph({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 48 48" aria-hidden="true" focusable="false" className={className} fill="currentColor">
      <ellipse cx="24" cy="11" rx="5.5" ry="8" />
      <ellipse cx="24" cy="37" rx="5.5" ry="8" />
      <ellipse cx="11" cy="24" rx="8" ry="5.5" />
      <ellipse cx="37" cy="24" rx="8" ry="5.5" />
      <circle cx="24" cy="24" r="5" fill="#f59e0b" />
    </svg>
  );
}
