# ADR-0009: Oracle Database with Oracle.EntityFrameworkCore

**Status:** Accepted (stakeholder requirement, Stage 2) — supersedes the PostgreSQL choice in ADR-0002.

## Context
The stakeholder mandated Oracle Database with PL/SQL where justified. No PostgreSQL code, migrations or data existed when the switch was made, so nothing had to be migrated.

## Decision

### Versions (verified against Oracle's documentation and NuGet metadata, 2026-10-13)
| Component | Version | Evidence |
|---|---|---|
| .NET | 10 LTS | "ODP.NET Core 23.26.0 and higher support .NET 10" (ODP.NET system requirements) |
| EF Core | 10.0.12 | Oracle.EntityFrameworkCore 10.23.26301 nuspec: `Microsoft.EntityFrameworkCore.Relational [10.0.0, 11.0.0)` |
| Oracle.EntityFrameworkCore | 10.23.26301 | "Oracle Entity Framework Core 23.26.0 and higher support Entity Framework Core 10" |
| Oracle.ManagedDataAccess.Core | 23.26.301 | Required minimum of the provider version above |
| Oracle Database | 19c or later | "Access to Oracle Database 19c or later" (ODP.NET Core requirements) |

- **SQL compatibility pinned to 19c** (`UseOracleSQLCompatibility(DatabaseVersion19)`), so generated SQL and column types are identical on 19c, 21c, 23ai and 26ai. For example, `bool` maps to `NUMBER(1)` rather than the 23ai `BOOLEAN` type. Development uses the locally installed XE 21c; Docker and CI use Oracle Database Free (26ai). Recommended production targets are **19c (long-term support) or 26ai**. 21c is an innovation release, acceptable for development only.
- **Accounts:** `CICEK_OWNER` owns all objects and is used only by the DbMigrator. `CICEK_APP` (API runtime) has `CREATE SESSION` + `CICEK_APP_ROLE`, owns nothing and cannot run DDL. The API qualifies objects with `Database:Schema` (EF `HasDefaultSchema`, part of the model cache key). Neither account is SYS/SYSTEM.
- **Module separation:** one owner schema, module table prefixes (`IDN_` …). In Oracle a schema is a user, so schema-per-module would multiply accounts and grants for no benefit.
- **Deployment:** migrations are applied only by the DbMigrator (`migrate`), never at API startup. The sequence is: migrations → `cicek_schema_access.grant_runtime_privileges` → role seeding (`DatabaseDeployment`, shared with tests).
- **Naming:** upper snake case for all identifiers, with constraint and index names prefixed by table (Oracle requires per-schema uniqueness). EF quotes identifiers, and upper-case names stay usable unquoted in SQL and PL/SQL.
- **Types:** `Guid` → `RAW(16)` (generated in C#); `DateTimeOffset` (UTC) → `TIMESTAMP(7) WITH TIME ZONE`; `decimal` → `NUMBER(19,4)` by convention; strings → `NVARCHAR2(n)` with explicit lengths (enforced by a model test; `max_string_size=STANDARD` caps NVARCHAR2 at 2000 characters); enums → `VARCHAR2` + CHECK.
- **Concurrency:** Oracle has no automatically maintained row version, so versioned entities carry a `ROW_VERSION NUMBER(19)` shadow property incremented in `SaveChanges`.
- **Retries:** no automatic retrying execution strategy, because a retried unit of work may already have committed. Deadlocks and serialization failures surface as HTTP 503 with Retry-After.
- **Pooling:** ODP.NET pooling on; bounded `MaxPoolSize`, `ConnectionLifeTime` 30 min, no per-checkout validation. Dead connections surface as ORA-03113/03135, which are classified as "Unavailable".

## Provider limitations found or confirmed (with mitigations)
| Limitation | Source | Mitigation |
|---|---|---|
| Filtered indexes (`HasFilter`) unsupported | Provider README; Identity emitted them | Naming convention clears filters; Oracle unique indexes already ignore all-NULL keys. A model test prevents regressions |
| CHECK constraint literals not escaped inside generated `EXECUTE IMMEDIATE '…'` | **Found by running the first migration** (PLS-00103) | `OracleCheckConstraints.InList` doubles quotes; restricted to enum names |
| `DateOnly`/`TimeOnly` unsupported | Provider README | Value converter to `DATE` when first needed (ADR-0005 amendment) |
| Some LINQ shapes (UPDATE…FROM, correlated MAX) fail on ≤21c | Provider README | Keep set-based updates simple; covered by integration tests per feature |
| **Oracle stores an empty string as NULL** | **Found by the Stage 3 integration tests** (ORA-01400 inserting a shop with an empty description) | Optional text is nullable and the domain stores `null` for blank values (`Guard.OptionalText`); migration `NullableOptionalText` |
| DDL auto-commits, so a failed migration can be partially applied | Oracle semantics; observed on the first failed run | Review `dotnet ef migrations script` output; apply to a clean schema in CI first; recovery procedure in docs/oracle-database.md |

## Consequences
+ Least-privilege runtime access that is verified by tests (the runtime user gets ORA-01031 on DDL).
+ Identical behaviour across supported Oracle releases.
− Oracle-specific SQL in a few places (check constraints, PL/SQL), isolated in Infrastructure.
− Local Docker Oracle is heavy (multi-GB image). Developers may use a native install instead.
