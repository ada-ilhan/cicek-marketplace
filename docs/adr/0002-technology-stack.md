# ADR-0002: Technology stack and dependency policy

**Status:** Proposed — *database row superseded by ADR-0009 (Oracle)*

## Decision
| Concern | Choice | Rationale |
|---|---|---|
| Runtime | .NET 10 (LTS), C# with nullable + warnings-as-errors | Latest LTS installed |
| Data | ~~PostgreSQL 17, Npgsql~~ → **Oracle Database (19c+), EF Core 10, Oracle.EntityFrameworkCore, ODP.NET Core** (ADR-0009) | Required stack |
| Validation | FluentValidation | Mature, Apache-2.0 |
| OpenAPI | Built-in `Microsoft.AspNetCore.OpenApi` + a UI in non-prod | Avoids extra generator dependency |
| Errors | Built-in ProblemDetails + `IExceptionHandler` | Framework-native |
| Rate limiting | Built-in `Microsoft.AspNetCore.RateLimiting` | Framework-native |
| Logging/metrics | `Microsoft.Extensions.Logging` JSON console + OpenTelemetry (traces, metrics) | Vendor-neutral |
| Background jobs | `BackgroundService` + database tables (outbox; Oracle supports `FOR UPDATE SKIP LOCKED`) | No broker until needed |
| Object storage | S3-compatible client (local emulator chosen with the catalog module) | Portable |
| Tests | xUnit v3 (Microsoft.Testing.Platform), WebApplicationFactory, a dedicated Oracle TEST schema | Real Oracle in integration tests; no in-memory substitute |
| Frontend | Next.js 16 (App Router), TypeScript 5.9 strict, Tailwind 4, TanStack Query, zod 4, openapi-typescript + openapi-fetch. *react-hook-form and next-intl deferred until forms/locale routing justify them (ADR-0011).* | Typed end to end |
| Frontend tests | Vitest + Testing Library, Playwright | Standard |

Policy: central package management, exact pinned versions, no package added without a stated need. MediatR and AutoMapper are deliberately excluded (licensing changes; explicit code is clearer at this size).
