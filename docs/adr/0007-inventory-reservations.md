# ADR-0007: Inventory as daily capacity with atomic conditional reservations

**Status:** Proposed

## Context
Florists make arrangements to order, so the binding constraint is "how many of this arrangement can this store produce for date D", not warehouse stock.

## Decision
- `daily_capacity(offer_id, date, capacity, reserved, sold)` with `CHECK (reserved + sold <= capacity)`.
- Reserve with a single conditional statement inside the checkout transaction:
  `UPDATE … SET reserved = reserved + @q WHERE offer_id = @o AND date = @d AND reserved + sold + @q <= capacity` → 0 rows affected means unavailable. No read-then-write race.
- Reservations expire after a configurable TTL (default 15 min). The expiry job releases them, and on payment confirmation they are committed (`reserved → sold`).
- Late payment after expiry: re-reserve or automatically void/refund (see ARCHITECTURE §4.5).

## Consequences
Correct under concurrency without serializable isolation. This will be verified by a parallel-reservation integration test against real Oracle (the conditional `UPDATE … WHERE` pattern works unchanged under Oracle read-committed isolation; ADR-0009).
