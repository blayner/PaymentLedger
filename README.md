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
tests/
  PaymentLedger.Domain.Tests/
  PaymentLedger.Application.Tests/
  PaymentLedger.Integration.Tests/   (needs Docker)
```

## Running

With Docker (API + PostgreSQL):

```bash
docker compose up --build
# API on http://localhost:5080, OpenAPI document at /openapi/v1.json
```

Without Docker, the API falls back to the in-memory event store:

```bash
dotnet run --project src/PaymentLedger.Api
```

`requests.http` has a walkthrough of the main endpoints.
