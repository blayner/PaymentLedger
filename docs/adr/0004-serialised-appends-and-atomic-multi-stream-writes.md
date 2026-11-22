# ADR-0004: Serialised appends and atomic multi-stream writes instead of a saga

- Status: Accepted
- Date: 2026-05-20

## Context

A transfer changes two aggregates. Classic DDD guidance is one aggregate per transaction, with a
saga (debit source, then credit destination, compensate on failure). That introduces an
intermediate state where money has left one account and not reached the other, plus compensation
logic that must itself be reliable.

Separately, projections tail the log by `global_position` ([ADR-0002](0002-cqrs-with-projections-from-the-global-log.md)).
In PostgreSQL, identity values are allocated when a row is inserted, not when the transaction
commits. If transaction A takes position 10 and B takes 11, and B commits first, a reader can see
11, advance its checkpoint, and never see 10.

## Decision

- `IEventStore.AppendAsync` takes several streams and writes them in **one database transaction**,
  with an expected version per stream. A transfer posts both sides atomically, so no saga is needed
  for internal transfers.
- Every append first takes `pg_advisory_xact_lock(<constant>)`. Appends are serialised, so
  positions are allocated and committed in the same order and the global log has no "late" gaps.
- `(stream_id, version)` is unique, as a safety net under the lock.
- Optimistic concurrency conflicts (stale reads) are retried by `ConcurrencyRetryBehavior` for
  commands that opt in with `IRetryOnConcurrency`. Their handlers reload state on every attempt and
  are idempotent on their transaction id.
- Retries use capped exponential backoff with full jitter (window 5 ms → 250 ms, up to 8 attempts).

> **Update (2026-11):** the first version used a linear 1–10·n ms jitter with 5 attempts. Running the
> integration suite against Postgres in Docker Desktop on Windows exhausted that budget on the hot
> settlement stream and returned 409 for valid deposits. The budget now grows exponentially and is
> capped, which keeps the common case fast while giving contended writers room to drain.

## Consequences

- No partial transfers and no compensation code.
- Write throughput is capped by one lock. That is a deliberate trade for a simple, gap-free order.
  Next steps if it becomes the bottleneck: one lock per currency (with per-currency logs), or a
  store with native ordered appends.
- The per-currency settlement stream sees the most conflicts. The retry counter
  (`ledger.concurrency.retries`) makes this visible; splitting settlement into sub-accounts would
  spread it.
- Atomic multi-stream writes only work while all streams live in one database, which holds for
  this service. Cross-service transfers would need a saga.
