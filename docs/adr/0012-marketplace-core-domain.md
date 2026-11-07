# ADR-0012: Marketplace core domain model (Stage 3)

**Status:** Accepted for Stage 3. Several points are assumptions awaiting business confirmation (marked ⚑).

## Context
Stage 3 implements shops, catalogue, cart, checkout and orders without a payment provider or courier integration. The Stage 1 business questions (§7 of ARCHITECTURE) are still open, so decisions favour models that can be extended without data migration.

## Decisions

1. **Shop-owned listings.** A product belongs to exactly one shop. Similar products from different shops are separate records. Stage 1's platform-product + store-offer model is deferred until a shared catalogue is actually needed. Variants (size, colour) are also deferred.
2. **Multi-shop cart, split at checkout** ⚑. A `Checkout` (customer level: idempotency, single currency, future payment) owns one `Order` per shop. This corresponds to Stage 1's Order (→ `Checkout`) and StoreOrder (→ `Order`); the per-shop order is the unit shops act on and the unit customers track.
3. **Stock is an integer per product**, reserved at checkout by one conditional `UPDATE … WHERE STOCK_QUANTITY >= :q` per line inside the checkout transaction, backed by a `CHECK (STOCK_QUANTITY >= 0)` constraint. It is released on cancel, reject and expiry. Daily production capacity (ADR-0007) remains a possible refinement. It was not chosen because the business has not confirmed how florists plan capacity.
4. **Delivery fee: flat per shop order, from configuration** ⚑; zones later. **Commission base excludes the delivery fee** ⚑ (the platform arranges delivery).
5. **Commission as a pure domain rule** (`CommissionCalculator`): basis points, shop override or platform default, commission rounded half away from zero to minor units, shop gets the exact remainder. Rate, base, commission and earnings are **snapshotted on each order**, so later rate changes never affect placed orders. Database CHECK constraints guard the arithmetic.
6. **No payment provider:** orders start `PendingPayment`. An administrator can record a manual payment (audited). Unpaid orders expire after a configurable timeout and release stock. `RefundDue` is recorded when a paid order ends without delivery; executing refunds and settlements is later work.
7. **Images by HTTPS URL** with a configurable host allowlist. File upload and object storage are deferred: storing binaries in Oracle is avoided, and the platform's media storage has not been chosen.
8. **Authorization:** platform permissions for administrators (`shops.manage`, `categories.manage`, `orders.read`, `orders.manage`). Shop-scoped rights come from `SHP_SHOP_MEMBERS` (Owner/Staff) and are checked in the application layer for every shop endpoint. Cross-customer access returns 404, so resource existence is not revealed.
9. **Persistence access:** the Application layer uses `IAppDbContext` (EF Core abstractions, per ADR-0003). Oracle-specific behaviour stays in Infrastructure (`ProductStock`, naming, constraints). The architecture test was narrowed to forbid `Microsoft.EntityFrameworkCore.Relational`, `Oracle.*` and ASP.NET Core in Application (it previously forbade all of EF Core, which was stricter than ADR-0003).
10. **No new PL/SQL for business logic.** Stock reservation is a single conditional SQL statement issued by EF Core. The only PL/SQL change is `cicek_schema_access` v2, which makes `AUD_AUDIT_ENTRIES` and `ORD_ORDER_STATUS_HISTORY` append-only for the runtime role (ADR-0010, reason 5).

## Consequences
+ Correct under concurrency (verified: 6 parallel checkouts for stock 2 → exactly 2 orders; 12 parallel reservations for stock 3 → exactly 3).
+ Placed orders are immutable financial records with database-enforced arithmetic.
− Customers comparing the "same" bouquet across shops see separate listings.
− Without online payment, orders need manual confirmation; this is acceptable only until a provider is integrated.

## Amendment (2026-10-19)
The stock reservation now also compares the price the order was quoted at and requires an active shop, inside the same conditional UPDATE (`StockReservation` result: Reserved, InsufficientStock, PriceChanged, Unavailable). This closes the window in which a price edit between reading the cart and reserving stock could sell at the old price. Verified by Oracle integration tests, including full rollback of earlier reserved lines.
