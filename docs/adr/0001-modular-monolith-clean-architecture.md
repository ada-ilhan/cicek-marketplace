# ADR-0001: Modular monolith with pragmatic Clean Architecture

**Status:** Proposed

## Context
This is a single product with one team. Checkout must reserve inventory and create the order atomically. There is no demonstrated need for independent scaling or deployment.

## Decision
- One deployable API (background jobs run in-process; a config switch allows a separate worker deployment of the same code later).
- Projects per **layer** (SharedKernel, Domain, Application, Infrastructure, Api), with **modules as namespaces** inside each.
- One database, one `DbContext`. *(Amended by ADR-0009: Oracle, one schema owner with module table prefixes instead of a schema per module.)*
- Cross-module references use IDs only. Cross-module side effects use domain events through the outbox.
- Boundaries are enforced by architecture tests (layer direction; no module referencing another module's internals).

## Consequences
+ Real ACID transactions where needed; simple local development and deployment.
+ A module can later be extracted along its schema and event boundaries.
− Module discipline depends on tests and review, not compiler-enforced project references.
