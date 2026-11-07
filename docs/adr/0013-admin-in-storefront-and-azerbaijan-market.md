# ADR-0013: Administrator area inside the storefront app; Azerbaijan market defaults

**Status:** Accepted (MVP stage)

## Decisions
1. **Administrator panel lives at `/admin` in `apps/storefront`**, like the seller area (ADR-0011 amendment), instead of the separate `apps/backoffice` planned in ADR-0008. It reuses the BFF, session cookies, UI package and typed client. A separate app/domain can be split out later; the screens only depend on API permissions, not on app structure. Trade-off: admin code ships in the same deployable (it is only reachable with a session and is authorized by the API), and the CSP/headers are shared.
2. **Authorization stays on the server.** The UI loads `/me` and shows only the navigation the caller's permissions allow, with an "Access denied" page otherwise. Permission strings are mirrored in `features/admin/permissions.ts` (a plain module: server components cannot use values exported from `"use client"` files). Roles are never granted by public registration.
3. **Shop lifecycle gains `Rejected`:** `PendingApproval → Rejected` (admin, reason required) `→ PendingApproval` (owner resubmit). Approval cannot skip resubmission. The CHECK constraint was changed with raw SQL because the Oracle provider emits invalid `ALTER TABLE … CHECK` quoting (guarded by `MigrationSafetyTests`).
4. **Market defaults:** Azerbaijani (Latin, `az`) is the primary UI language; Turkish and English are kept. Defaults are `Marketplace:Currency=AZN`, `TimeZone=Asia/Baku`; the code stays configuration-driven. Seed category names are Azerbaijani (names only; slugs are stable identifiers). Backend tests pin TRY/Istanbul explicitly.
5. **Dashboard money is never mixed across currencies:** totals include only orders in the marketplace currency.
6. **Payment/delivery honesty:** orders show "payment not received" until an administrator records a manual payment (labelled as not taking a real payment, audited); delivery progress is labelled as reported by the florist, not courier tracking.
7. **Demo data is explicit and development-only:** `DbMigrator seed-demo` requires `CICEK_ALLOW_DEMO_SEED=true`, refuses Production, takes passwords only from `CICEK_DEMO_PASSWORD`, uses `@example.test` addresses, creates no images, and is idempotent.

## Consequences
+ One session/BFF, one design system; admin works end to end without Swagger.
− Same deployable and headers as the public site; the category-name migration overwrites renamed seed categories.
