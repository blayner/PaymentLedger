# ADR-0002: CQRS with projections built from the global log

- Status: Accepted
- Date: 2026-04-12

## Context

Queries such as "ledger entries for an account, newest first" or "trial balance per currency" are
expensive to answer by replaying aggregate streams. They need denormalised read models.

The obvious way to feed projections, publishing in-process notifications after each save, breaks
under concurrency: two commits on the same account can finish in one order and notify in the
other, and a projection that applies them out of order ends up with a stale balance.

## Decision

- Commands and queries are separate messages with separate handlers (`ICommand`/`IQuery`).
- Projections consume the **global event log** ordered by `global_position`, from a checkpoint,
  through a single `ProjectionRunner` that applies events one at a time.
- After a successful append, the repository triggers a catch-up so the writing instance has
  read-your-writes. A background poller picks up writes from other instances.
- Account balance queries that feed decisions read the event stream directly (strong consistency);
  listing and reporting queries read projections (eventual consistency).

## Consequences

- Projections always see events in commit order and can be rebuilt from position zero.
- The ordering guarantee depends on `global_position` being committed in order
  ([ADR-0004](0004-serialised-appends-and-atomic-multi-stream-writes.md)).
- Read models are currently in-memory per instance and rebuilt at start-up. Persisted read models
  with a stored checkpoint are the upgrade path for large logs.
- A projection failure after a durable append does not fail the command; it is logged and retried
  by the poller.
