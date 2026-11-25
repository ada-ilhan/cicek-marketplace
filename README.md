# Çiçek — Flower Delivery Marketplace

| Stage | State |
|---|---|
| 1. Architecture | Documented ([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)); business questions in §7 still open |
| 2. Foundation (Oracle) | **Implemented** |
| 3. Core marketplace | **Implemented**: shops, catalogue, cart, checkout, orders, commission (no payment/courier integration) |
| MVP: customer, florist and administrator panels | **Implemented** (Azerbaijani/AZN defaults; admin at `/admin`) — see [PROJECT_STATUS.md](docs/PROJECT_STATUS.md) |
| 4+. Payment provider, courier, notifications | Not started (intentionally out of MVP scope) |

Docs: [Project status (TR)](docs/PROJECT_STATUS.md) · [MVP gap analysis (TR)](docs/MVP_GAP_ANALYSIS.md) · [Progress (TR)](docs/DEVELOPMENT_PROGRESS.md) · [Marketplace core](docs/marketplace-core.md) · [Architecture](docs/ARCHITECTURE.md) · [Oracle Database guide](docs/oracle-database.md) · [ADRs](docs/adr/README.md)

## Repository layout

```
backend/    ASP.NET Core API, DbMigrator, Clean Architecture projects, tests, OpenAPI document (openapi/)
frontend/   npm workspace: apps/storefront (Next.js), packages/ui, packages/api-client
db/         DBA bootstrap script (Oracle accounts and roles)
deploy/     Docker Compose stack and Dockerfiles
docs/       Architecture, Oracle guide, ADRs
```

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 10.0.300+ (pinned in `backend/global.json`) |
| Node.js | 26 LTS (`frontend/.nvmrc`; CI and the storefront image use Node 26; Node 22 is no longer validated) |
| Oracle Database | 19c or later, with a PDB you can create users in; **or** Docker for Oracle Database Free |
| SQL*Plus or SQLcl | for the one-time bootstrap |
| Docker + Compose | optional, for the containerized stack |

## 1. Database setup (once)

Create the least-privilege accounts as a DBA (passwords are prompted, never stored):

```bash
sqlplus system@//localhost:1521/XEPDB1 @db/bootstrap/create_schema_users.sql CICEK_OWNER CICEK_APP CICEK_APP_ROLE USERS
```

Store the passwords as user-secrets (development) or environment variables (`Database__Password`):

```bash
cd backend
dotnet user-secrets set Database:Password <CICEK_OWNER password> --id cicek-dbmigrator-dev
dotnet user-secrets set Database:Password <CICEK_APP password>   --id cicek-api-dev
dotnet user-secrets set Auth:Jwt:SigningKey "$(openssl rand -base64 48)" --id cicek-api-dev
```

`DataSource` defaults to `localhost:1521/XEPDB1` (see `appsettings.json`); override it with `Database__DataSource`. All keys are listed in [docs/oracle-database.md §3](docs/oracle-database.md#3-connection-configuration).

## 2. Migrations

```bash
cd backend
dotnet tool restore
dotnet run --project src/Cicek.DbMigrator -- migrate      # migrations + runtime grants + roles
dotnet run --project src/Cicek.DbMigrator -- status
CICEK_ADMIN_PASSWORD='<min 10 chars>' dotnet run --project src/Cicek.DbMigrator -- create-admin --email admin@example.com
```

Adding and reviewing migrations: [docs/oracle-database.md §4](docs/oracle-database.md#4-schema-and-migrations). Do not use `dotnet ef database update`.

## 3. Run

```bash
# API → http://localhost:5080  (Swagger UI at /swagger in Development)
cd backend && dotnet run --project src/Cicek.Api --launch-profile http

# Storefront → http://localhost:3000
cd frontend
npm ci
cp apps/storefront/.env.example apps/storefront/.env.local   # adjust if needed
npm run dev
```

Health: `GET /health/live` (process up) and `GET /health/ready` (Oracle reachable).

### Marketplace configuration and workflow

Market settings live in the `Marketplace` section of `backend/src/Cicek.Api/appsettings.json` (currency, time zone, default commission, delivery fee, payment timeout, image host allowlist). See [docs/marketplace-core.md §4](docs/marketplace-core.md#4-configuration-marketplace-section). **Set `Marketplace__ImageHostAllowlist__0=<your CDN host>` in production.**

### Run the full MVP locally

```bash
# 0. Once: database accounts and secrets (sections 1-2 above), then migrate
cd backend && dotnet run --project src/Cicek.DbMigrator -- migrate

# 1. Development administrator (password from an environment variable, never an argument)
CICEK_ADMIN_PASSWORD='<min 10 chars>' dotnet run --project src/Cicek.DbMigrator -- create-admin --email admin@example.com

# 2. Optional, DEVELOPMENT ONLY: sample florists, products and a customer (refused in Production)
CICEK_ALLOW_DEMO_SEED=true CICEK_DEMO_PASSWORD='<min 10 chars>' DOTNET_ENVIRONMENT=Development \
  dotnet run --project src/Cicek.DbMigrator -- seed-demo
#    creates demo.customer@example.test and demo.florist1..3@example.test (all with CICEK_DEMO_PASSWORD) and 3 approved demo shops

# 3. API (http://localhost:5080) and storefront (http://localhost:3000), in two terminals
cd backend  && dotnet run --project src/Cicek.Api --launch-profile http
cd frontend && npm ci && npm run dev
```

**Images.** Product and florist images are uploaded through the UI (JPEG/PNG/WebP, 5 MB). In Development the API stores them on disk (`Media:Provider=Local`, folder `backend/.local-media`, git-ignored). **Production needs an object-storage adapter behind `IImageStorage` (not yet written) and `Marketplace:ImageHostAllowlist` for external image URLs**; with no provider configured, uploads answer 503, and with an empty allowlist external image URLs are rejected. See `docs/adr/0014-image-storage.md`.

The UI defaults to Azerbaijani (`NEXT_PUBLIC_DEFAULT_LOCALE=az`); the header has a language switcher (az / tr / en). Market defaults are AZN and Asia/Baku (`Marketplace` settings).

**Accounts and panels**

| Role | How to get it | Panel |
|---|---|---|
| Customer | Register at `/register` | `/products`, `/cart`, `/account/orders` |
| Florist | Any registered user applies at `/seller` (or use a demo florist) | `/seller`: shop profile, products, orders |
| Administrator | Only `DbMigrator create-admin` (public registration never grants it) | `/admin`: overview, florists, orders, categories, users |

**Approval and order workflow (all in the UI)**
1. A florist applies at `/seller`; the shop is *awaiting approval* and not public.
2. An administrator opens `/admin/shops?status=PendingApproval`, reviews the shop and **approves** or **rejects** (a reason is required). A rejected florist sees the reason, corrects the details and applies again.
3. The approved florist adds products. Customers find them, add them to the cart and check out (a multi-florist cart becomes one order per florist).
4. Orders start as *awaiting payment*. **No payment provider is integrated**: an administrator records a manual payment at `/admin/orders/{orderNumber}` (clearly labelled; it takes no money and is audited). The florist then accepts, prepares, marks ready and delivered (reported manually, not courier tracking). Unpaid orders expire after `PaymentTimeout` and release stock.

### Docker Compose (Oracle Database Free + migrator + API + storefront)

Last verified on 2026-11-03 (Docker Desktop, Windows 11): clean start, migrations, health checks, the full Playwright suite (16/16) and a restart that kept the data, on Oracle AI Database 26ai Free (`container-registry.oracle.com/database/free:latest`) and, in an isolated project, on Oracle Database XE 21c (`express:21.3.0-xe`). The remote GitHub Actions run has not happened yet.

```bash
cp deploy/.env.example deploy/.env            # git-ignored; set the three Oracle passwords (letters, digits, _) and JWT_SIGNING_KEY (>= 32 random chars)
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
docker compose -f deploy/docker-compose.yml --env-file deploy/.env ps   # wait until oracle, api and storefront are "healthy"; migrator exits 0
```

| Service | Port | Health |
|---|---|---|
| oracle | host 1522 → 1521 | image check **and** the schema owner exists (so the migrator cannot race the bootstrap) |
| migrator | none (one-shot) | `service_completed_successfully` gates the API |
| api | 5080 → 8080 | `/health/ready` (database reachable) |
| storefront | 3000 | HTTP 200 on `/login`; starts after the API is healthy |

- The first start downloads the Oracle image (~2 GB) and takes a few minutes. Data lives in the named volumes `cicek_oracle-data` and `cicek_media-data`; **never use `docker compose down -v`** unless you intend to erase them. `docker compose stop` / `restart` keep the data.
- `deploy/oracle/setup/01_create_cicek_users.sh` runs on every Oracle start (idempotent) and creates `CICEK_OWNER`, `CICEK_APP` and `CICEK_APP_ROLE`. It is mounted as a *startup* script because current Oracle Free images ship a pre-built database, so the image's first-creation "setup" hook never runs.
- Create an administrator: `docker compose -f deploy/docker-compose.yml --env-file deploy/.env run --rm -e CICEK_ADMIN_PASSWORD=<password> migrator create-admin --email you@example.com`.
- The stack runs the API with `ASPNETCORE_ENVIRONMENT=Development` (override with `ASPNETCORE_ENVIRONMENT` in `deploy/.env`). Production-like environments refuse to start with `Marketplace__AllowAnyExternalImageHost=true` or `Media__Provider=Local`, and without a valid `Auth__Jwt__SigningKey`.
- For automated end-to-end runs only, add `AUTH_RATE_LIMIT=1000` to `deploy/.env` (default 10 sign-in/registration requests per minute per IP) and run `E2E_BASE_URL=http://localhost:3000 E2E_FULL_STACK=1 E2E_ADMIN_EMAIL=... E2E_ADMIN_PASSWORD=... npx playwright test` from `frontend/apps/storefront`. The tests create users, florists and orders, so only do this against this disposable stack.
- **Shutting down.** The Oracle service has `stop_grace_period: 2m`, so `docker compose stop` / `restart` waits up to two minutes for the database to flush and close cleanly (observed: with Docker's default 10 s timeout the Oracle container was killed with exit code 137 during local testing; configured: 2 minutes, intended to give Oracle time to close cleanly; **not yet verified**: that a stop with the 2-minute limit really ends with exit code 0). The setting applies to a container only once it has been recreated (`docker compose up -d`); a container created before that keeps the old 10 s limit. Never use `down -v`: it deletes the data volumes.
- **Isolated end-to-end stack (verified 2026-11-03).** `deploy/scripts/e2e-stack.sh [project]` starts a separate Compose project (own volumes, ports 1622/5180/3100, generated throwaway secrets), waits on the real health checks, creates an administrator and runs the whole Playwright suite; it is also what the CI job `compose-e2e` runs. It never deletes anything. Oracle XE 21c: `ORACLE_IMAGE=container-registry.oracle.com/database/express:21.3.0-xe ORACLE_PDB=XEPDB1 ORACLE_HOST_PORT=1624 API_HOST_PORT=5182 STOREFRONT_HOST_PORT=3102 deploy/scripts/e2e-stack.sh cicek-xe` (about 15 GB image, about 2 GB RAM). Remote CI needs a GitHub repository with the baseline pushed; no repository secrets are used.
- Port already in use? Stop the other process or change the left-hand port in `deploy/docker-compose.yml`; the API must stay reachable from the storefront container as `api:8080`.

## 4. Tests and checks

```bash
# Backend: format, build (warnings = errors), all tests
cd backend
dotnet format Cicek.slnx --verify-no-changes
dotnet build Cicek.slnx
dotnet test --solution Cicek.slnx
```

Oracle integration tests need a **TEST** schema pair (names must contain `_TEST`; the fixture wipes that schema):

```bash
sqlplus system@//localhost:1521/XEPDB1 @db/bootstrap/create_schema_users.sql CICEK_TEST_OWNER CICEK_TEST_APP CICEK_TEST_APP_ROLE USERS
dotnet user-secrets set TestDatabase:OwnerPassword <pwd> --id cicek-tests-dev
dotnet user-secrets set TestDatabase:AppPassword   <pwd> --id cicek-tests-dev
```

Without these settings, Oracle tests are reported as **skipped**.

```bash
# Frontend
cd frontend
npm run generate:api      # regenerate types after API changes (the API build writes backend/openapi/cicek-api.json)
npm run format:check && npm run lint && npm run typecheck && npm test && npm run build
npx --workspace @cicek/storefront playwright install chromium
npx --workspace @cicek/storefront playwright test --project=chrome   # real Chrome: Azerbaijani formatting (needs Google Chrome installed)
npm run test:e2e                                  # smoke tests (starts the built storefront; no API needed)
# Full journey through the UI for all three roles (API must be running; use an administrator from create-admin):
E2E_FULL_STACK=1 E2E_ADMIN_EMAIL=<admin email> E2E_ADMIN_PASSWORD=<admin password> npm run test:e2e
# Tests pin the UI language to English via a cookie; one test checks the Azerbaijani default.
```

CI: `.github/workflows/ci.yml` runs four jobs; Dependabot (`.github/dependabot.yml`) proposes weekly updates.

| Job | What it checks |
|---|---|
| `backend` | NuGet vulnerability scan, `dotnet format`, warnings-as-errors build, all tests against an Oracle Database Free service container, OpenAPI document up to date |
| `frontend` | production `npm audit`, generated API client up to date, format, lint, typecheck, unit tests, build, Playwright smoke tests |
| `docker-images` | builds the API, migrator and storefront images (not pushed) and validates the Compose file |
| `compose-e2e` | starts the whole stack with `deploy/scripts/e2e-stack.sh` and runs the full Playwright suite (all roles) |

The workflow has not run on GitHub yet; the same steps were verified locally on 2026-11-03.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| API log: `Oracle database … is unreachable (ORA-12541)` | Listener not running or wrong host/port in `Database__DataSource` |
| `ORA-12514` | Wrong service name (XE: `XEPDB1`, Database Free: `FREEPDB1`) |
| `ORA-01017` | Wrong username or password in the configuration |
| `ORA-50000` | Connection pool timeout: database slow or unreachable |
| `ORA-00942` from the API after a new migration | Grants not synchronized: run the DbMigrator, not `dotnet ef database update` |
| Migrator: "Database:Schema is set" | The migrator must connect as the owner with `Database__Schema` empty |
| Validation error on startup naming `Database:*` or `Auth:Jwt:SigningKey` | Missing secret; see section 1 |
| Bootstrap fails with `ORA-00972` when piping passwords from Windows PowerShell | PowerShell adds a byte-order mark; type the passwords at the prompt or use Git Bash |
| `dotnet test` says VSTest is not supported | Use the .NET 10 SDK; `global.json` selects Microsoft.Testing.Platform |
