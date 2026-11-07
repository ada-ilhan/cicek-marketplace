# Architecture Decision Records

Format: short context → decision → consequences. Status values: Proposed, Accepted, Superseded.

| # | Title | Status |
|---|---|---|
| [0001](0001-modular-monolith-clean-architecture.md) | Modular monolith with pragmatic Clean Architecture | Proposed (amended by 0009) |
| [0002](0002-technology-stack.md) | Technology stack and dependency policy | Proposed (database row superseded by 0009) |
| [0003](0003-use-case-handlers-without-mediator.md) | Plain use-case handlers, no mediator library | Implemented (Stages 2–3) |
| [0004](0004-authentication.md) | Authentication: ASP.NET Core Identity + JWT + rotating refresh tokens behind a BFF | Proposed — implemented in Stage 2 |
| [0005](0005-money-and-time.md) | Money, currency and time representation | Proposed (amended for Oracle) |
| [0006](0006-reliability-outbox-inbox-idempotency.md) | Transactional outbox, webhook inbox, idempotency keys | Proposed — not yet implemented |
| [0007](0007-inventory-reservations.md) | Inventory as daily capacity with atomic conditional reservations | Conditional reservation implemented on integer stock (ADR-0012); daily capacity deferred |
| [0008](0008-frontend-structure.md) | Frontend: two Next.js apps in an npm workspace | Proposed (staged by 0011) |
| [0009](0009-oracle-database.md) | Oracle Database with Oracle.EntityFrameworkCore | Accepted |
| [0010](0010-plsql-usage-policy.md) | PL/SQL usage policy | Accepted |
| [0011](0011-frontend-foundation.md) | Frontend foundation choices (Stage 2) | Accepted (amended in Stage 3) |
| [0012](0012-marketplace-core-domain.md) | Marketplace core domain model (Stage 3) | Accepted (with flagged assumptions) |
| [0013](0013-admin-in-storefront-and-azerbaijan-market.md) | Admin area inside the storefront; Azerbaijan market defaults | Accepted |
| [0014](0014-image-storage.md) | Image storage and validation (Stage 5) | Accepted |
