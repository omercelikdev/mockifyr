# Mockifyr

**An independent, .NET-based API mock engine + platform.** A functional alternative to
WireMock — but a completely independent codebase and its own IP. WireMock / WireMock.Net are
not used as dependencies.

> **Status:** architecture / pre-code. No implementation yet.
> Direction: [ARCHITECTURE.md](ARCHITECTURE.md) · roadmap:
> [docs/roadmap.md](docs/roadmap.md) · decisions: [docs/decisions/](docs/decisions/) ·
> learned WireMock behavior: [docs/parity/](docs/parity/).
>
> This is an **AI-driven repository** — see [CLAUDE.md](CLAUDE.md) for how work is done here.

---

## What it does

The core engine answers a single question: *"Given the loaded stubs, which one matches this
request and what should it return?"* — without knowing anything about HTTP, ports, or
transport. Thin facades sit on top:

- **Library** — in-process (use inside tests)
- **HTTP server** — standalone / container
- **Admin REST API** — runtime stub management

First vertical: **request matching + response templating + webhook/callback correlation** —
each verified against real WireMock (the oracle) via **differential / golden-master testing**.

## Design principles

- **Transport-agnostic core** — matching/templating never leaks into a transport.
- **First-class multi-tenancy** — one process/port, N tenants; logical isolation.
- **Pure, deterministic engine** — no I/O; delay/fault/proxy are directives, outbound is a
  listener.
- **Green differential diff is the only "done"** — the oracle is always running WireMock; no
  self-validation.
- **CQRS (Mediant)** only on the management path; the hot path stays direct and
  allocation-lean.

## Repository layout

```
src/        engine, capabilities, application (CQRS), facades, host
harness/    differential test harness + fuzzing generator
tests/      unit + differential suites
docs/       roadmap, architecture decisions (ADR), parity knowledge
```

Full topology and dependency rules: [ARCHITECTURE.md](ARCHITECTURE.md#3-solution-topology).

## Roadmap (summary)

- **Phase A — narrow vertical:** G0 foundation+harness → G1 matching → G2 templating →
  G3 webhooks.
- **Phase B — toward parity:** faults, scenarios, verify/near-miss, admin API, proxy,
  record/playback, extensibility, HTTPS, deploy, gRPC/GraphQL, persistence providers.
- **Post-phase:** UI / dashboard.

Full list: [docs/roadmap.md](docs/roadmap.md).

## Technology

.NET 10 (LTS) · C# · CQRS via [Mediant](https://github.com/omercelikdev/mediant) ·
differential oracle: Java WireMock (Testcontainers).

## License

TBD (commercializable IP — license not yet decided).
