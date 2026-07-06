# Mockifyr

**An independent, .NET-based API mock engine + platform.** A transport-agnostic matching and
response engine with first-class multi-tenancy, pluggable persistence, and thin facades
(library / HTTP server / admin REST / gRPC / GraphQL / WebSocket). Mockifyr is a clean-room,
independent codebase with its own IP — it does **not** use WireMock or WireMock.Net as
dependencies. It is designed to interoperate with the WireMock JSON stub format and is proven
correct by differential testing against WireMock as a reference oracle (see [Trademarks](#trademarks)).

> **Status:** engine + platform complete. All roadmap groups **G1–G16** are implemented and
> validated; the remaining work is the **UI / dashboard**.
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
- **Post-phase:** UI / dashboard (the only remaining work).

Full list: [docs/roadmap.md](docs/roadmap.md).

## Persistence

The hot path is always **in-memory** — matching never touches a database. Durability is an
opt-in seam (`IStubPersistence`); pick one backend and mutations write through to it while the
in-memory store serves reads:

| Flag | Backend |
|------|---------|
| *(none)* | in-memory only (ephemeral, the default) |
| `--root-dir <dir>` | file-based JSON mappings |
| `--litedb <path>` | LiteDB (embedded single-file) |
| `--postgres <connstr>` | PostgreSQL |
| `--redis <connstr>` | Redis |

`--change-feed` (Redis / Postgres) keeps multiple instances coherent without a restart.

## Technology

.NET 10 (LTS) · C# · CQRS via [Mediant](https://github.com/omercelikdev/mediant) ·
differential oracle: Java WireMock (Testcontainers).

## License

Licensed under the **Apache License, Version 2.0** — see [LICENSE](LICENSE) and [NOTICE](NOTICE).

## Trademarks

WireMock is a trademark of WireMock Inc. **Mockifyr is an independent project and is not
affiliated with, endorsed by, or sponsored by WireMock Inc.** Mockifyr is a clean-room
implementation and does not use WireMock or WireMock.Net as dependencies. References to
"WireMock" in this repository are nominative and descriptive only — for **interoperability**
(Mockifyr imports the WireMock JSON stub format) and **differential testing** (Mockifyr's
correctness is verified against real WireMock as a reference oracle; the oracle code lives in
the test/harness projects and is not part of the distributed product). All other product names
and brands are the property of their respective owners.
