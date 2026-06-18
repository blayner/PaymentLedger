# ADR-0005: Transactional outbox for integration events

- Status: Accepted
- Date: 2026-04-28

## Context

Other systems (notifications, fraud scoring, accounting exports) need to learn about ledger
changes. Publishing to a broker after the database commit loses messages if the process dies in
between; publishing before the commit announces changes that might roll back. Distributed
transactions across the database and the broker are not an option.

## Decision

- Every appended event produces an `outbox_messages` row **in the same transaction** as the event.
- `OutboxProcessor` (a `BackgroundService`) polls the outbox and hands messages to an
  `IIntegrationEventPublisher`. The store claims rows with `FOR UPDATE SKIP LOCKED`, so one processor
  per API instance can run without double-publishing a batch in the normal case.
- Delivery is **at-least-once**. The outbox message id equals the domain event id, which consumers
  use to deduplicate.
- Failures increment `attempts`; messages past `MaxAttempts` are dead-lettered (left in place) for
  manual inspection, and the readiness check reports `Degraded`.

## Consequences

- No event is lost once its transaction commits.
- Consumers must be idempotent, which they need to be anyway with most brokers.
- The claim is held while publishing. A slow broker holds row locks longer; batch size bounds that.
- Messages carry domain events as they are. If external consumers need a stable public contract,
  a mapping step from domain events to integration events belongs in the publisher.
- The default publisher logs messages; a broker adapter is a single interface implementation.
