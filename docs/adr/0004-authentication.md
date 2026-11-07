# ADR-0004: Authentication

**Status:** Proposed

## Decision
- **ASP.NET Core Identity** for the user store, password hashing (PBKDF2, framework default), lockout, and email/phone tokens.
- API issues **short-lived JWT access tokens** (~10 min) and **rotating refresh tokens** (stored hashed, with a family id; reuse revokes the whole family).
- Browsers never see tokens: each Next.js app acts as a **BFF**. Its route handlers store tokens in `HttpOnly; Secure; SameSite=Lax` cookies and attach a Bearer token when calling the API. Mutating BFF routes verify `Origin`, which with SameSite cookies covers CSRF.
- Authorization: roles (`Customer`, `FloristOwner`, `FloristStaff`, `Admin`, `Support`, `Finance`) map to **permissions**. Store-scoped permissions come from `StoreMembership` and are checked by a resource-based authorization handler on every florist endpoint.
- Sensitive admin actions are written to an append-only audit log.

## Alternatives
OpenIddict/full OIDC server: deferred until third-party clients or native mobile apps need standard OAuth flows. The token service sits behind an interface, so it can be swapped.
