# ADR-0006: In-house dispatcher (no MediatR) and permissively licensed test libraries

- Status: Accepted
- Date: 2026-04-05

## Context

Handlers need a dispatcher plus a pipeline for cross-cutting behaviour (telemetry, validation,
concurrency retries). MediatR is the usual choice in .NET, but versions from 13 onwards are
commercially licensed. FluentAssertions made the same move from version 8.

The project targets .NET 10 / C# 14 and should stay free to build, fork and run.

## Decision

- **No MediatR.** `PaymentLedger.Application.Messaging` contains a small dispatcher (under 200 lines):
  `ICommand<T>`/`IQuery<T>` messages, `ICommandHandler`/`IQueryHandler`, open-generic
  `IPipelineBehavior<TRequest, TResponse>`, and `AddMessaging(assembly, behaviours...)`, which
  scans handlers and registers behaviours in order (first registered = outermost).
  Licensing aside, it removes a dependency for a few dozen lines of code we fully understand.
- **Tests use xunit.v3 and Shouldly** instead of FluentAssertions.
- The OpenAPI document comes from the built-in `Microsoft.AspNetCore.OpenApi` package, not Swashbuckle.

## Consequences

- No commercial licences are needed to build or run the project.
- The dispatcher is deliberately minimal: no notifications or streaming. Anything more would be
  added only when needed.
- The handler wrapper for each request type is built once with reflection and cached, so dispatch
  costs one dictionary lookup plus DI resolution.
