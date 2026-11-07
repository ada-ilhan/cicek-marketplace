# ADR-0010: PL/SQL usage policy

**Status:** Accepted

## Context
The stack mandates PL/SQL "where justified". Moving business workflows into the database would split rules between two languages, weaken unit testing, and duplicate validation.

## Decision
**Default:** business rules and workflows live in C# (Domain/Application). PL/SQL is allowed only with a documented reason from this list:
1. Atomic multi-row operations that are measurably simpler or safer in one database call.
2. Bulk or set-based operations shown to benefit from database-side execution.
3. Database-centric reporting.
4. Invariants that must hold for every access path (when constraints cannot express them).
5. Operations that must use the owning schema's dictionary or rights (e.g. privilege management).

**Conventions (mandatory):**
- Named packages, one responsibility each; specification and body in separate, versioned files under `backend/src/Cicek.Infrastructure/Persistence/Sql/Packages/<package>/vN/`. Released versions are immutable; a change ships as `vN+1` plus a new EF migration. `Down()` restores the previous version or drops the package.
- Deployed by EF migrations (`migrationBuilder.Sql(SqlResources.Read(...))`) so schema and code versions stay in lockstep.
- Explicit `IN`/`OUT` parameters, called from C# with named binds (`BindByName = true`). No dynamic SQL built from untrusted input. Identifiers go through `DBMS_ASSERT`, and keywords are whitelisted.
- **Transactions are owned by the caller.** Packages must not `COMMIT`/`ROLLBACK`. The single exception is DDL (which commits implicitly); such procedures are documented as "must not run inside a transaction" and the C# caller checks for this.
- **Error contract:** `RAISE_APPLICATION_ERROR(-20nnn, '<module>.<code>: <text>')`. Each package documents its codes in its specification. `OracleDatabaseErrorClassifier` extracts `<module>.<code>`, and the API returns it as HTTP 422 with `code`, never the ORA text. Unexpected errors propagate; nothing is swallowed.
- No triggers for application workflows.
- Every package has Oracle integration tests (behaviour and error contract).

## Current packages
| Package | Reason (from list) | Contract |
|---|---|---|
| `cicek_schema_access` v1 | 5 — derives grants from `USER_TABLES`/`USER_VIEWS`/`USER_SEQUENCES`/`USER_OBJECTS` and issues GRANT/REVOKE as owner | `grant_runtime_privileges(p_grantee IN, p_changes OUT)`; errors -20001..-20003; DDL, so caller must not hold a transaction |

## Consequences
+ Database code is rare, reviewed and tested; no duplicated business rules.
− Some future operations (e.g. inventory reservation) will need a measured decision between a conditional `UPDATE` from C# (the default, ADR-0007) and a package.
