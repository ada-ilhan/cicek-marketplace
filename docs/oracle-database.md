# Oracle Database Guide

Audience: developers and DBAs working on the Çiçek backend. Decisions and their rationale are in [ADR-0009](adr/0009-oracle-database.md) and [ADR-0010](adr/0010-plsql-usage-policy.md).

## 1. Supported versions

| Component | Version |
|---|---|
| Oracle Database | **19c or later**. SQL is generated for 19c compatibility. Verified on **21c XE** (local). Docker/CI use **Oracle Database Free** (26ai). Production recommendation: 19c or 26ai. |
| Oracle.EntityFrameworkCore | 10.23.26301 |
| Oracle.ManagedDataAccess.Core (ODP.NET Core) | 23.26.301 |
| EF Core | 10.0.12 |
| .NET | 10 LTS |

Character set: the database must use **AL32UTF8**, which is required for Turkish text; verified on the local XE. `MAX_STRING_SIZE=STANDARD` is assumed, so `NVARCHAR2` holds at most 2000 characters and longer text needs `NCLOB`.

## 2. Accounts and privileges

| Account | Purpose | Privileges |
|---|---|---|
| `CICEK_OWNER` | Owns every table, index and package. Used **only** by the DbMigrator during deployments. | `CREATE SESSION, CREATE TABLE, CREATE SEQUENCE, CREATE VIEW, CREATE PROCEDURE`, quota on the data tablespace. No `ANY` privileges, no `CREATE TRIGGER`. |
| `CICEK_APP_ROLE` | Runtime object privileges | Granted by `cicek_schema_access` (v2) after each migration: tables `SELECT/INSERT/UPDATE/DELETE`; `EF_MIGRATIONS_HISTORY` `SELECT` only; **`AUD_AUDIT_ENTRIES` and `ORD_ORDER_STATUS_HISTORY` `SELECT/INSERT` only (append-only)**; views and sequences `SELECT`; packages `EXECUTE` (except the deployment package). |
| `CICEK_APP` | API runtime connection | `CREATE SESSION` + `CICEK_APP_ROLE`. Owns nothing, quota 0, cannot run DDL. |

Never connect the application as `SYS`, `SYSTEM` or `PDBADMIN`. In production, lock `CICEK_OWNER` between deployments (`ALTER USER cicek_owner ACCOUNT LOCK`) and unlock it only for the migration window.

### Creating the accounts (once per environment, by a DBA)

```bash
sqlplus system@//<host>:<port>/<pdb_service> @db/bootstrap/create_schema_users.sql CICEK_OWNER CICEK_APP CICEK_APP_ROLE USERS
```

The script prompts for both passwords (hidden), is idempotent, and validates names with `DBMS_ASSERT`. Use letters, digits and `_` in passwords; a double quote is rejected. For a separate **test** schema pair, run it again with names containing `_TEST`, e.g. `CICEK_TEST_OWNER CICEK_TEST_APP CICEK_TEST_APP_ROLE`. The integration tests refuse to reset any schema without `_TEST` in its name.

> Windows PowerShell 5.1 adds a byte-order mark when piping text to native programs, which corrupts piped passwords. Type them at the prompt, or pipe from Git Bash.

## 3. Connection configuration

Configuration section `Database` (environment variables use `__`, e.g. `Database__Password`):

| Key | API (runtime) | DbMigrator (owner) | Notes |
|---|---|---|---|
| `DataSource` | `localhost:1521/XEPDB1` | same | EZConnect `host:port/service`, a TNS alias, or a full descriptor |
| `UserId` | `CICEK_APP` | `CICEK_OWNER` | |
| `Password` | secret | secret | user-secrets (dev), environment or secret manager (prod). **Never in appsettings.** |
| `Schema` | `CICEK_OWNER` | *(empty)* | Owner schema used to qualify objects for the runtime user. The migrator refuses to run when it is set. |
| `MinPoolSize` / `MaxPoolSize` | 1 / 50 | | ODP.NET pool per process |
| `ConnectionTimeoutSeconds` | 15 | | Pool wait / connect timeout |
| `CommandTimeoutSeconds` | 30 | | EF Core command timeout |
| `ConnectionLifetimeSeconds` | 1800 | | Recycles pooled connections (failover, firewalls) |

The connection string is built with `OracleConnectionStringBuilder`, so values cannot inject extra attributes, and `Persist Security Info=false`. Credentials never appear in logs: the API was checked for this with the database both up and down.

**Startup behaviour:** missing settings fail fast with a validation error naming the key. An unreachable database does **not** stop the API. `/health/live` stays 200, `/health/ready` returns 503, and the log reports the ORA code and which settings to check. Requests that need the database return a generic 503 with `Retry-After`.

## 4. Schema and migrations

- EF Core migrations live in `backend/src/Cicek.Infrastructure/Persistence/Migrations` and are committed.
- Naming: upper snake case (`IDN_USERS.NORMALIZED_EMAIL`). Module prefixes: `IDN_` for Identity; later modules add their own.
- Type rules (enforced by `ModelConventionTests`): every string has a max length; decimals have precision; no `DateTime` (use UTC `DateTimeOffset`); no filtered indexes; identifiers ≤128 bytes and unique.
- **Empty strings:** Oracle stores `''` as NULL. Optional text columns are therefore nullable and the domain stores `null` for blank input (`Guard.OptionalText`). Never declare an optional text column NOT NULL. This was found by the Stage 3 integration tests (ORA-01400).
- **Financial invariants** are also CHECK constraints (`CK_ORD_ORDERS_TOTAL`, `CK_ORD_ORDERS_COMMISSION_SPLIT`, `CK_CAT_PRODUCTS_STOCK`), so they hold for any access path.

### Commands (from `backend/`)

```bash
dotnet tool restore
# Add a migration (no database needed):
dotnet ef migrations add <Name> --project src/Cicek.Infrastructure --startup-project src/Cicek.DbMigrator --output-dir Persistence/Migrations
# Inspect the SQL before applying (review every migration):
dotnet ef migrations script <From> <To> --project src/Cicek.Infrastructure --startup-project src/Cicek.DbMigrator -o migration.sql
# Apply (as owner) + sync runtime grants + seed roles:
dotnet run --project src/Cicek.DbMigrator -- migrate
# Status:
dotnet run --project src/Cicek.DbMigrator -- status
# Administrator account (password via environment variable, never as an argument):
CICEK_ADMIN_PASSWORD=... dotnet run --project src/Cicek.DbMigrator -- create-admin --email admin@example.com
```

Do **not** use `dotnet ef database update`: it skips the grant synchronisation, so the runtime user would lack privileges on new tables.

### Safety rules
- The API never applies migrations at startup.
- **Oracle DDL commits implicitly.** A migration that fails part-way can remain partially applied, and its history row will be missing. This was observed during development: a CHECK-constraint quoting bug left three tables behind. Mitigations:
  1. Every migration runs against a clean TEST schema in CI (the integration-test fixture applies all migrations).
  2. Review the `migrations script` output before production.
  3. Recovery: inspect `USER_OBJECTS`, drop only the objects created by the failed migration (by name), fix the migration, and re-run. Never drop a schema to recover.
- Destructive changes (dropping or narrowing columns) need an expand/contract plan: add the new column → backfill → switch code → drop in a later release.
- Rollback: prefer roll-forward migrations. `migrations script <Current> <Previous>` generates `Down()` SQL for review. Data-losing `Down()` steps must never run in production without a backup.

## 5. SQL and PL/SQL organisation

| Path | Contents | Applied by |
|---|---|---|
| `db/bootstrap/create_schema_users.sql` | Accounts and roles (DBA, once) | SQL*Plus / SQLcl by a DBA |
| `backend/src/Cicek.Infrastructure/Persistence/Sql/Packages/<pkg>/vN/package_{spec,body}.sql` | Versioned PL/SQL packages (embedded resources) | EF migrations |
| `deploy/oracle/setup/*.sh` | Container-only wrapper around the bootstrap script | Oracle Database Free image on first start |

Package rules (ADR-0010): one responsibility per package; explicit parameters; caller-owned transactions (no COMMIT); `DBMS_ASSERT` on identifiers; error contract `RAISE_APPLICATION_ERROR(-20nnn, '<module>.<code>: …')`; immutable released versions; integration tests per package.

### `cicek_schema_access` (v1)
`grant_runtime_privileges(p_grantee IN VARCHAR2, p_changes OUT PLS_INTEGER)` brings the grantee's object privileges to exactly the policy in §2. It revokes extras and grants missing privileges, so it is idempotent: a second run reports 0 changes. It issues DDL and therefore **must not be called inside a transaction**; the C# caller enforces this. Errors: `-20001 schema_access.invalid_grantee`, `-20002 schema_access.grantee_not_found`, `-20003 schema_access.invalid_privilege`.

## 6. Transactions and concurrency conventions

- Oracle's default isolation (READ COMMITTED, writers don't block readers) is used. `SERIALIZABLE` is not used by default, because it raises ORA-08177 under contention.
- One `SaveChanges` is one transaction. Use cases that write through several components open an explicit transaction (e.g. user + role on registration).
- Optimistic concurrency: `ROW_VERSION NUMBER(19)` shadow column, incremented by `AppDbContext`. A conflict raises `ConcurrencyConflictException` in the Application layer and HTTP 409 at the API. Verified with two concurrent refresh-token rotations: exactly one wins.
- No automatic retries of whole units of work: a retried request may already have committed. Deadlocks (ORA-00060) and serialization failures return 503 + `Retry-After`. Future checkout and payment operations will combine retries with idempotency keys (ADR-0006).
- External systems (payment, courier) are never part of a database transaction (ARCHITECTURE §4.5).

### Error translation (Infrastructure → API)
| Oracle | Classification | HTTP |
|---|---|---|
| ORA-00001 | UniqueViolation | 409 `conflict.duplicate` |
| ORA-02291 / 02292 | ReferenceViolation | 409 `conflict.reference` |
| ORA-00060 / 08177 | TransientContention | 503 `service.busy` |
| ORA-03113/03114/03135, ORA-12xxx TNS, ORA-01017, ORA-28000, ORA-50000 (pool timeout), ORA-01013 | Unavailable | 503 `service.unavailable` |
| ORA-50201 wrapping an inner ORA-50232 | Unavailable | 503 `service.unavailable` |
| ORA-20000…20999 with contract code | ApplicationRule | 422 with the contract code |

**ORA-50201 / ORA-50232 (managed ODP.NET driver).** ORA-50201 ("Oracle Communication") is a general wrapper that the driver raises for several client-side problems, including an unparsable connect string. It is therefore *not* classified as Unavailable on its own and stays Unknown (500 `server.error`). ORA-50232 ("Network Transport: TCP transport address connect failure") identifies a failed TCP connection to the database host. When an ORA-50201 exception has an inner exception whose message starts with the ORA-50232 number, the failure is Unavailable. This chain was observed with the Linux build of the driver against an unreachable host/port (GitHub Actions, Linux runner); other driver versions or platforms may report a different chain, in which case the failure stays Unknown until its codes are verified and added. Only the leading error number of the inner message is read, never the descriptive text.
| EF concurrency exception | ConcurrencyConflict | 409 `conflict.concurrent_update` |
| anything else | Unknown | 500 `server.error` |

Responses never include ORA messages, SQL, object names or connection details.

## 7. Development and testing setup

**Native install (used during Stage 2):** Oracle Database 21c XE on Windows, PDB `XEPDB1`, port 1521.

**Docker:** `deploy/docker-compose.yml` runs `container-registry.oracle.com/database/free` on host port **1522** (avoiding a native listener on 1521) with a persistent volume. The image's own health check gates the migrator, and the bootstrap runs automatically on first creation. The first start takes several minutes; "healthy" means the database is open. See the README for the commands.

**Tests:** `Cicek.Infrastructure.Tests` and `Cicek.Api.Tests` run against the TEST schema pair. Configure them with user-secrets id `cicek-tests-dev` or environment variables:

```bash
dotnet user-secrets set TestDatabase:OwnerPassword <pwd> --id cicek-tests-dev
dotnet user-secrets set TestDatabase:AppPassword <pwd> --id cicek-tests-dev
# optional: TestDatabase:DataSource (default localhost:1521/XEPDB1), OwnerUser, AppUser, RuntimeRole
```

The fixture takes a cross-process lock, drops every object in the TEST owner schema (guarded by the `_TEST` name check in C# and in PL/SQL), then runs the production `DatabaseDeployment`. Tests marked as Oracle tests are **skipped** (not passed) when no test database is configured. In-memory databases are not used as evidence of Oracle behaviour.

## 8. Backup and recovery considerations

- Production: RMAN with archivelog mode, regular level 0/1 backups and archived-log backups to separate storage; test restores regularly. Point-in-time recovery requires archivelog mode.
- Logical exports (`expdp schemas=CICEK_OWNER`) are useful for copying environments, but they are **not** a substitute for RMAN.
- Always take a backup or restore point immediately before production migrations: `CREATE RESTORE POINT before_<release> GUARANTEE FLASHBACK DATABASE` where Flashback is enabled.
- XE and Database Free are for development only (resource caps, no support).
- Financial and audit records are never hard-deleted (ARCHITECTURE §3); retention periods per market remain an open question.
