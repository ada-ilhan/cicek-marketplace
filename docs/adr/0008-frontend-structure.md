# ADR-0008: Frontend — two Next.js apps in an npm workspace

**Status:** Proposed

## Decision
- `apps/storefront`: public, SEO and performance focused (server components, cached catalog pages).
- `apps/backoffice`: florist and admin dashboards (authenticated, data-table heavy), deployable on a separate subdomain with stricter headers.
- Shared `packages/ui` (accessible Tailwind components), `packages/api-client` (types generated from the API's OpenAPI document, typed fetch, central Problem Details → UI error mapping), and `packages/config`.
- Server state lives in TanStack Query (client) or server components. Local UI state stays in components. Route guards are UX only; the API authorizes every request.
- No hardcoded API URLs, currencies or locales: these come from env and `/markets/current`, and formatting uses `Intl`.

## Consequences
+ Admin code never ships to public visitors; independent deploys.
− Two apps to build in CI (shared packages keep duplication low).
