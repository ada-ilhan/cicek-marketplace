# ADR-0005: Money, currency and time

**Status:** Proposed

## Decision
- `Money(decimal Amount, Currency Currency)` value object. Arithmetic across different currencies throws. Rounding uses the currency's minor units with `MidpointRounding.AwayFromZero` (configurable per market).
- Persisted as `NUMBER(19,4)` plus a `CHAR(3)` currency code (Oracle, ADR-0009). `float`/`double` are banned for money (enforced by an architecture test).
- Commission rates are stored as integer basis points.
- All instants are `DateTimeOffset` in UTC (`TIMESTAMP(7) WITH TIME ZONE`). Delivery dates are calendar dates in the **market's timezone**, because "deliver on Feb 14" is a local calendar concept. Time comes from an injected `IClock`/`TimeProvider`.
- Order lines snapshot price, tax rate, discount and commission at purchase time.

## Amendment (Stage 2, ADR-0009)
The Oracle EF Core provider does not support `DateOnly`/`TimeOnly`. Calendar dates will be mapped to Oracle `DATE` through a value converter (domain keeps a date-only value type), introduced with the first module that needs them.
