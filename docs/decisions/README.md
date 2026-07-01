# Architecture Decision Records (ADR)

This directory keeps Mockifyr's architectural decisions as short, traceable records. Each ADR
documents a decision, its context, and its consequences. Format: context → decision →
consequences.

| # | Decision | Status |
|---|----------|--------|
| [0001](0001-transport-agnostic-core.md) | Transport-agnostic core engine + thin facades | Accepted |
| [0002](0002-differential-oracle-wiremock.md) | Differential validation, oracle = Java WireMock | Accepted |
| [0003](0003-first-class-multi-tenancy.md) | First-class multi-tenancy (logical isolation) | Accepted |
| [0004](0004-own-model-wiremock-adapter.md) | Own domain model + WireMock JSON import adapter | Accepted |
| [0005](0005-cqrs-mediant-application-layer.md) | CQRS via Mediant, application layer only | Accepted |
| [0006](0006-pluggable-persistence-inmemory-hotpath.md) | Pluggable persistence; in-memory hot-path SoT | Accepted |

When adding a decision, use the next number and add a row to this table.
