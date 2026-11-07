# ADR-0003: Plain use-case handlers, no mediator library

**Status:** Proposed

## Decision
Each use case is a small sealed class (for example, `AcceptStoreOrderHandler`) with a `HandleAsync(command, CancellationToken)` method. It is registered in DI and injected directly into the endpoint. Cross-cutting concerns (validation, transaction, audit) are applied by explicit, small helpers or endpoint filters.

The Application layer reads and writes through an `IAppDbContext` abstraction that exposes `DbSet`s. Dedicated repositories exist only where they encapsulate a non-trivial persistence rule (inventory conditional updates, order-number allocation, outbox claiming).

## Consequences
+ "Go to definition" works; no reflection-based dispatch; no licensing risk.
− Application references `Microsoft.EntityFrameworkCore` abstractions. Domain stays EF-free.
