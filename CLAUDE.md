# CLAUDE.md — Operating Manual for Mockifyr

This repository is **AI-driven**: most work is done by an AI agent (Claude) under human
review. This file is the contract for how that work happens. Read it before touching
anything. Keep it up to date — when a rule changes, change it here.

---

## 0. What Mockifyr is

An independent, .NET-based API mock **engine + platform** — a functional alternative to
WireMock with an entirely independent codebase (WireMock / WireMock.Net are **not**
dependencies). The core engine is transport-agnostic; thin facades (Library / HTTP server /
Admin REST) sit on top. Correctness is proven by **differential testing against real
WireMock**, never by self-assessment.

- **Design & rationale:** [ARCHITECTURE.md](ARCHITECTURE.md)
- **Roadmap (living checklist):** [docs/roadmap.md](docs/roadmap.md)
- **Architecture decisions:** [docs/decisions/](docs/decisions/)
- **Learned WireMock behavior (parity knowledge):** [docs/parity/](docs/parity/)

---

## 1. Language policy (non-negotiable)

- **Everything committed to this repo is in English** — source code, comments, XML doc
  comments, identifiers, commit messages, PR descriptions, all markdown docs, ADRs, and this
  file.
- Conversation with the maintainer may be in Turkish. **Never** let Turkish leak into repo
  artifacts.

---

## 2. Golden rules (guardrails)

1. **Narrow-vertical discipline.** Build only the current roadmap item. No "while I'm here"
   scope creep. Feature parity arrives gradually and validated, never all at once.
2. **Green differential diff is the only definition of done.** "Looks like it works" is not
   done. The oracle is running Java WireMock — not the model's memory, not a self-written
   assertion of what "should" happen.
3. **No self-validated tests.** Never approve your own output against your own assumption of
   correctness. The behavioral truth is the oracle.
4. **The engine stays pure.** `Mockifyr.Core` has zero external dependencies, does no I/O,
   and never references a transport, a mediator, or a persistence library. Delay/fault/proxy
   are **directives** the facade applies; outbound calls go through `IServeEventListener`.
5. **Transport never leaks into the engine.** Matching/templating logic lives behind Core
   contracts, never inside an HTTP handler.
6. **Multi-tenancy is first-class.** Every store/engine entry point takes an explicit
   `TenantId`. There is no tenant-less overload. Forgetting scope must be a compile error.
7. **No over-engineering.** Only the abstraction this vertical needs. The exceptions
   (multi-tenancy, persistence seam, extension seams) are deliberate because retrofitting
   them is the expensive path.
8. **Stop at every checkpoint.** Commit + short progress summary + wait for approval. No
   autonomous drift across roadmap items.

---

## 3. The development loop (per roadmap item)

Every item in [docs/roadmap.md](docs/roadmap.md) is developed the same way:

1. **Pick** the next unchecked item. Do not start the next one until the current is green.
2. **Failing test first (TDD).** Author the scenario as **WireMock JSON** (the single source
   of truth), load it raw into Java WireMock and via the import adapter into Mockifyr, drive
   the same request through both, and assert the diff. It must fail first.
3. **Implement minimally** — just enough to satisfy this item.
4. **Green diff.** The differential harness reports byte/semantic parity after
   canonicalization + volatile-field masking.
5. **Feed the docs (the learning step — do not skip):**
   - Record every non-obvious WireMock behavior discovered from the diff in
     `docs/parity/<group>.md` (e.g. how `equalToJson` treats nested array order). This is how
     the repo *learns*: assumptions become verified, durable facts.
   - Tick the checkbox in `docs/roadmap.md`.
   - If a design decision was made or changed, add/update an ADR in `docs/decisions/`.
6. **Commit** (see §5) and post a short summary. **Stop for approval.**

> The parity knowledge base (`docs/parity/`) is the memory of this project. Anything that
> surprised us about WireMock goes there so the next item builds on evidence, not guesswork.

---

## 4. Where things go (repo map)

```
src/
  Mockifyr.Core/                 domain model + contracts + pure StubEngine (zero deps)
  Mockifyr.Matching/             IMatcher implementations
  Mockifyr.Templating/           Handlebars.Net renderer + ITemplateHelper set
  Mockifyr.Stores.InMemory/      tenant-scoped in-memory stores
  Mockifyr.Adapters.WireMockJson/ WireMock JSON <-> domain model import adapter
  Mockifyr.ServeEvents.Webhook/  IServeEventListener impl (outbound I/O)
  Mockifyr.Application/          CQRS handlers (Mediant) — MANAGEMENT PATH ONLY
  Mockifyr.Facade.Library/       in-process API
  Mockifyr.Facade.Http/          Kestrel mock server, tenant resolution, wire delivery
  Mockifyr.Facade.Admin/         /__admin/* REST (thin: HTTP -> CQRS dispatch)
  Mockifyr.Facade.Grpc/          gRPC serving (protobuf <-> JSON codec -> engine)
  Mockifyr.Server/               composition root (host, config, CLI)
harness/
  Mockifyr.Differential.Harness/   Java WireMock (Testcontainers) + canonical diff
  Mockifyr.Differential.Generator/ deterministic property-based/fuzzing generator
tests/                            unit + differential suites
docs/                             roadmap, decisions (ADR), parity knowledge
```

Dependency rule: **all arrows point inward to Core.** No facade depends on another facade.
External libraries (Handlebars.Net, JSONPath, XML, Kestrel, Mediant, Testcontainers) live
only at the edges. Mediant appears **only** in `Mockifyr.Application`.

---

## 5. Conventions

- **.NET 10** (LTS), C#. `Nullable` enabled, implicit usings off unless justified,
  file-scoped namespaces, `var` when the type is apparent.
- **Naming:** interfaces `I`-prefixed; async members return `Task`/`ValueTask` and end in
  `Async` except pipeline hooks that mirror Mediant's `Handle`.
- **Application layer** uses Mediant's `ICommand<T>`/`IQuery<T>` and the `Result<T>` pattern —
  no exceptions for control flow.
- **Commits:** Conventional Commits, English, imperative mood, e.g.
  `feat(matching): add urlPathTemplate named path variables`. Reference the roadmap item id
  (G1b) in the body. End co-authored commits with the required trailer.
- **Tests:** name by behavior and by oracle expectation. Differential tests are the primary
  safety net; unit tests cover pure logic and tenant-isolation invariants (which the oracle
  cannot check).

---

## 6. Build & test

Requires the .NET 10 SDK (pinned in `global.json`). NuGet restores from nuget.org only
(`nuget.config`); versions are centralized in `Directory.Packages.props`; shared MSBuild
settings live in `Directory.Build.props` (net10.0, nullable, warnings-as-errors).

```bash
dotnet build Mockifyr.sln -c Debug          # build all 16 projects
dotnet test  Mockifyr.sln -c Debug          # run unit tests
dotnet run   --project src/Mockifyr.Server  # run the standalone host (placeholder)
```

Differential tests (`tests/Mockifyr.Differential.Tests`) require Docker to run the Java
WireMock oracle; they are added from G0/G1a. CI (`.github/workflows/ci.yml`) builds and runs
the unit tests on every PR.

---

## 7. Current status

G0 done and G1 in progress. The engine serves via exact/standard matching (URL/method plus
the `equalTo`/`equalToIgnoreCase`/`contains`/`matches`/`doesNotMatch`/`absent` value matchers on
headers/query/cookies/body), static responses, in-memory tenant-scoped stores, and the WireMock
JSON import adapter. The differential harness runs the real WireMock oracle via Testcontainers.
The **fuzzing generator** (`MatcherScenarios`) emits hundreds of seed-driven corpus probes and
the property suite asserts every match decision agrees with the oracle — it has already caught a
real divergence (empty request body is "absent" for body matching). Builds clean (0 warnings);
unit 16/16; differential 18/18 (incl. property tests). Next: finish G1 (cookie/doesNotMatch/
multi-value/binaryEqualTo), then G1e `equalToJson` (fuzz-validated), then G1f `matchesJsonPath`.
