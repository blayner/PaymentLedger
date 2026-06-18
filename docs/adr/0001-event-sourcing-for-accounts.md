# ADR-0001: Event sourcing for accounts

- Status: Accepted
- Date: 2026-03-28

## Context

A ledger must answer "what was the balance, and why" for any point in time. Auditors, dispute
handling and reconciliation all need the full sequence of changes, not just the current state.
A state-based model with an audit table keeps two sources of truth that can drift apart.

## Decision

`Account` is an event-sourced aggregate. Its state is a left fold over its own stream
(`AccountCreatedEvent`, `FundsDepositedEvent`, `TransferInitiatedEvent`, ...). Behaviour methods
validate invariants and raise events; `When` is the only place state changes.

Every balance-changing event records its ledger side (`Debit`/`Credit`) and the resulting balance,
so consumers never need to re-derive accounting rules.

The store is append-only, with one stream per account and optimistic concurrency on the stream
version.

## Consequences

- The audit trail *is* the data model: history cannot be lost or edited.
- Read models can be rebuilt or new ones added by replaying the log ([ADR-0002](0002-cqrs-with-projections-from-the-global-log.md)).
- Idempotency comes naturally: the aggregate remembers which transaction ids it has applied.
- Event schemas are a public contract. Renaming or changing an event requires an upcaster;
  persisted type names are short, stable names rather than CLR full names, to allow refactoring namespaces.
- Long streams make loading slower. Snapshots are not implemented yet (see Known limitations).
