# ADR-0007: Idempotency keys checked against request fingerprints

- Status: Accepted
- Date: 2026-10-29
- Amends: the idempotency notes in [ADR-0004](0004-serialised-appends-and-atomic-multi-stream-writes.md)

## Context

Money movements require an `Idempotency-Key` header, and the key becomes the transaction id.
Until now an account only remembered *which* transaction ids it had applied. A request whose key
was already applied got `200 OK` with `alreadyProcessed: true`, whatever its body said.

That is wrong when a client reuses a key for a different operation, which happens through bugs
such as a key generated once per screen instead of once per payment. A client that sends
"deposit 100" and then "deposit 250" with the same key is told the second call succeeded,
although nothing was posted. For a ledger, silently answering "done" for a movement that never
happened is the worst outcome.

The IETF draft *The Idempotency-Key HTTP Header Field* (draft-ietf-httpapi-idempotency-key-header)
and payment APIs such as Stripe solve this by remembering a fingerprint of the original request.
A retry must match it, and a key reused with another payload is an error.

Concurrency adds a second problem. A handler reads the primary account (customer, or transfer
source) and then the counterpart (settlement, or destination). If an identical request commits
between those two reads, the handler sees the key as new on the primary and already applied on
the counterpart. The domain's duplicate-posting guard then fires and the client gets a `422`
instead of the original result. The new concurrency test reproduced this reliably.

## Decision

1. **Fingerprint.** `RequestFingerprints` computes the SHA-256 (lowercase hex) of a canonical text
   form of the command:
   `v1 \n <operation> \n <account ids…> \n <amount> \n <currency>`.
   - The operation is `deposit`, `withdrawal` or `transfer`.
   - Account ids use the `D` format: the customer for deposits and withdrawals, source then
     destination for transfers.
   - The amount uses invariant `G29`, so `10.50` and `10.5` match.
   - The currency is trimmed and upper-cased.
2. **What is left out:**
   - the free-text `description`, which moves no money and is often regenerated on retry;
   - timestamps and anything server-generated;
   - the key itself, which is the lookup key, not part of the payload.
   The `v1` prefix lets the canonical form change later without new fingerprints accidentally
   matching old ones.
3. **Storage.** The fingerprint travels on the `Transaction` and is written to every
   `BalanceChangedEvent` (`RequestFingerprint`). Accounts rebuild a `transaction id → fingerprint`
   map when they replay their stream, so there is no new table, and the check works the same
   with the in-memory store and with PostgreSQL. The property is optional: events written
   before this change deserialize with `null`, and such transactions are accepted as replays.
4. **Classification** (`Account.CheckIdempotency`, wrapped by `IdempotencyGuard`):

   | Stored state for the key | Result |
   | --- | --- |
   | not applied | post the transaction (`201`) |
   | same fingerprint, or legacy `null` | replay: `200` with `alreadyProcessed: true`, nothing posted |
   | different fingerprint | `IdempotencyKeyReusedException` → `422` |

5. **Response for reuse:** `422 Unprocessable Content` as `application/problem+json`, with
   `type: urn:paymentledger:problem:idempotency-key-reused`, `code: idempotency.key_reused` and a
   detail that tells the client to use a new key or resend the original payload. The draft
   recommends 422 for this case. `409` is reserved here for a concurrent modification the client
   may simply retry, and reusing a key is not something a retry fixes.
6. **Counterpart check.** After loading the counterpart account, the handler checks the key there
   as well (`IdempotencyGuard.EnsureCounterpartIsConsistent`):
   - **Same fingerprint on the counterpart:** because the fingerprint contains both account ids,
     this can only be the identical request, which committed after our read of the primary.
     Both streams are appended atomically, so our copy of the primary is stale. The guard raises
     `ConcurrencyException`, and `ConcurrencyRetryBehavior` reruns the command: it reloads the
     primary, sees the key, and answers as a replay.
   - **Different fingerprint on the counterpart:** the key was already used for another request
     touching that account (for example a deposit to a different customer, through the shared
     settlement account). It is rejected with `422`.

## Concurrency

Two simultaneous requests with the same key never both post:

- Both read the accounts at some version and append with that expected version, under the
  advisory lock from ADR-0004. The first append wins. The second fails with
  `ConcurrencyException`, which the retry behaviour catches before reloading and re-classifying
  the request (replay or mismatch).
- If the second request read the primary before the first committed and the counterpart after, the
  counterpart check above turns that into the same conflict, retry and replay path.
- The domain's `transaction.duplicate` guard in `Account` remains as the last safety net.

The tests cover these cases:

- `Concurrent_requests_with_the_same_key_apply_once` (application, in-memory store);
- `Concurrent_requests_with_the_same_key_apply_exactly_once` (integration: 8 parallel HTTP
  requests against PostgreSQL, exactly one `201` and seven `200`).

## Consequences

- Clients get an explicit error for a reused key instead of a false success.
- No schema change: fingerprints are part of the event payload, and old events stay readable.
- Every balance-changed event grows by 64 characters.
- Changing which fields are fingerprinted requires a new version prefix. Otherwise retries of
  requests made before the change would be rejected.

## Limits and alternatives considered

- **Scope of keys.** Keys are remembered per account, not per API client. Reuse is caught when the
  two requests share an account (the settlement account covers deposits and withdrawals in the
  same currency), but a key reused for transfers between two unrelated pairs of accounts is not
  detected. A multi-tenant deployment would keep an `idempotency_keys` table keyed by
  `(client_id, key)` with the fingerprint, status and stored response, written in the same
  transaction as the events. That was not done here, because this service has no notion of API
  clients yet.
- **Replays return the current balance**, not a stored copy of the original response. Storing
  responses (as Stripe does) would make replays byte-for-byte identical, at the cost of that
  extra table.
- **Hashing the raw request body** was rejected. JSON property order, whitespace and number
  formatting (`10.5` vs `10.50`) would make equivalent retries look different.
- **No retention window yet.** Keys live as long as the account's history; snapshots plus a
  bounded window would cap memory for very long-lived accounts.
