# 0005 — CQRS via Mediant, application layer only

**Status:** Accepted · **Date:** 2026-07-01

## Context
Mockifyr has two very different execution paths: a high-throughput **hot path** (serving mock
requests, potentially thousands/sec) and a low-frequency **management path** (admin/runtime
operations: create/update/delete stubs, import mappings, reset journal, query requests). The
maintainer owns [Mediant](https://github.com/omercelikdev/mediant), an enterprise CQRS
mediator (a MediatR alternative with `ICommand<T>`/`IQuery<T>`, the `Result<T>` pattern,
pipeline behaviors, and `[HttpEndpoint]` mapping), and wants to dogfood it.

## Decision
Adopt **CQRS via Mediant for the management path only**, inside `Mockifyr.Application`. The
mock-serving hot path (`Facade.Http` → `StubEngine`) goes **direct**, with no mediator.
`Facade.Admin` stays thin: HTTP → `ISender.Send(command|query)` → `Mockifyr.Application`
handler → Core. Cross-cutting concerns (FluentValidation, audit/logging, idempotency, and a
custom tenant-scope guard behavior) run as Mediant pipeline behaviors.

## Consequences
- (+) The engine (the crown-jewel IP) stays dependency-free; `Mockifyr.Core`'s zero-dependency
  rule already forbids Mediant there. Mediant lives only in `Mockifyr.Application`.
- (+) The hot path remains direct and allocation-lean; mediator overhead is confined to
  low-frequency management operations.
- (+) Dogfoods Mediant broadly (Send/Publish, behaviors, FluentValidation, AspNetCore
  endpoints, later EF Core + Outbox with persistence providers) → real-world test coverage.
- (+) Future synergies: `Mediant.Behaviors/Outbox` + `IIdempotencyStore` for reliable webhook
  delivery (G3/G16); `IPublisher`/`IDomainEvent` for the future UI's live updates and the
  reload change-feed.
- (−) Mediant is currently `1.0.0-rc.3` (pre-release); depending on it couples release
  stability. **Mitigation:** Core is Mediant-free, so the engine is untouched if Mediant must
  be swapped or frozen. Acceptable because both are the maintainer's IP and dogfooding is a
  goal.
