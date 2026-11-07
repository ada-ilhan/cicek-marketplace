# ADR-0006: Transactional outbox, webhook inbox, idempotency keys

**Status:** Proposed

## Decision
- **Outbox:** domain events are serialized into `outbox_messages` in the same transaction as the state change. A background dispatcher claims batches with `FOR UPDATE SKIP LOCKED` and retries with exponential backoff. Messages that exceed max attempts are dead-lettered and surfaced in admin.
- **Inbox:** every incoming webhook is signature-verified, then inserted into `inbox_messages` with a unique `(provider, external_event_id)`. Duplicates return 200 without reprocessing.
- **Idempotency keys:** required on `POST /checkout` and payment/refund-initiating endpoints. Stored with a request hash; a reused key with a different body returns 422. Outgoing provider calls also carry our own idempotency key.
- **Reconciliation job:** periodically resolves `Processing`/stale payments and refunds by querying the provider.

## Consequences
At-least-once delivery everywhere, so every consumer must be idempotent. This is covered by tests.
