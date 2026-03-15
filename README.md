# PaymentLedger

Payment ledger built with Event Sourcing, CQRS and double-entry bookkeeping on .NET 10.

> Work in progress.

## Layout

```
src/
  PaymentLedger.Domain/          entities, value objects, domain events
  PaymentLedger.Application/     commands, queries, handlers
  PaymentLedger.Infrastructure/  event store, repositories, outbox
  PaymentLedger.Api/             HTTP API
```
