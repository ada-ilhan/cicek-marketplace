# ADR-0011: Frontend foundation choices (Stage 2)

**Status:** Accepted

## Decision
- **One app first.** `apps/storefront` is built now. `apps/backoffice` (ADR-0008) is created in Stage 4 together with the first florist/admin screens; shared code already lives in `packages/ui` and `packages/api-client`.
- **BFF (ADR-0004) implemented in route handlers:**
  - `/api/auth/{login,register,logout}` hold tokens in HttpOnly, SameSite=Lax cookies (`__Host-` prefix when secure).
  - `/api/bff/v1/*` forwards to the API with the bearer token and refreshes transparently once per request.
  - Mutating requests require a same-origin `Origin` (or `Sec-Fetch-Site: same-origin`).
  - The proxy only forwards `v1` paths, rejects traversal and the token endpoints, and passes `X-Forwarded-For` so the API rate-limits per client.
- **Typed client:** `openapi-typescript` generates `schema.d.ts` from the API's build-time OpenAPI document; `openapi-fetch` is used server-side. CI fails if the generated types are stale.
- **Errors:** every failure becomes an `ApiError` (`kind`, `code`, `fieldErrors`, `traceId`). UI text is chosen by kind/code from localized messages, never by showing raw server text.
- **i18n:** a small typed message catalog (`tr` default, `en`) with locale from a cookie or `NEXT_PUBLIC_DEFAULT_LOCALE`. `next-intl` is deferred until locale-prefixed routing is needed; the catalog shape is compatible with it.
- **Forms:** native forms + zod (same limits as the API). `react-hook-form` is deferred until forms become complex (checkout).
- **Toolchain pinned to Node 22.11 compatibility** (installed runtime): TypeScript 5.9 (not the 7.0 native compiler), ESLint 9, Vitest 3, jsdom 26, Vite 6. Upgrading Node to ≥22.13 (or 24 LTS) unblocks ESLint 10 / Vitest 5. ESLint 9 is now marked deprecated by its maintainers, so this upgrade is recommended before Stage 4.
- **Route guards are UX only** (`src/proxy.ts`, Next 16's renamed middleware); the API authorizes every request.

## Consequences
+ Tokens never reach browser JavaScript (verified by the Playwright full-stack test).
− The BFF is an extra hop; acceptable for a marketplace storefront.

## Amendment (Stage 3)
The seller area (shop application, product and order management) is built as `/seller` routes inside `apps/storefront`. It reuses the existing BFF/session and UI instead of duplicating them in a second app. The administrator UI is still planned as `apps/backoffice` (Stage 4); until then, administrator actions are performed through the API (Swagger UI in development).
