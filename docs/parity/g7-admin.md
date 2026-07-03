# Parity notes — G7 Admin API + stub metadata

WireMock admin behaviors probed against the oracle (`wiremock/wiremock:3.10.0`) plus notes on how the
management path is built. See [README](README.md) for the format.

## Admin CRUD shapes (probed from the oracle)

- **`POST /__admin/mappings`** creates a stub and returns it with an **`id`** and a duplicate
  **`uuid`** (the same GUID), the request/response, and any **`metadata`** — status **201**. The stub
  serves immediately.
- **`GET /__admin/mappings`** → `{"mappings":[ … ]}`, each entry carrying `id`/`uuid`/`metadata`.
- **`GET /__admin/mappings/{id}`** → 200 with the stub.
- **`DELETE /__admin/mappings/{id}`** → **200**; the stub stops serving afterwards. Idempotent.
- **`POST /__admin/mappings/reset`** clears every mapping.
- **Invalid stub JSON** → **422**.

## G7a — management path (CQRS via Mediant), validated in-process

- **Group / item:** G7a. The admin JSON is volatile-field-heavy (`id`/`uuid` differ per engine), so
  the HTTP surface is validated **semantically** in G7b; G7a validates the underlying **CQRS handlers
  in-process** (`Mockifyr.Application.Tests`).
- **CQRS.** `Mockifyr.Application` (Mediant 1.0.0, `ISender`, `Result<T>`): `CreateStubCommand`,
  `DeleteStubCommand`, `ImportMappingsCommand`, `ResetMappingsCommand`, and the queries
  `GetStubsQuery`/`GetStubQuery`/`CountRequestsQuery`/`FindUnmatchedRequestsQuery`. Handlers depend
  only on Core contracts (`IStubStore`) and the engine's read-only verify methods. Mediant lives
  **only** here (decision 0005).
- **Shared state.** `AddMockifyr` (composition root, `Mockifyr.Server`) registers the in-memory
  stores + engine + Mediant handlers as singletons, so a stub created through the **management path**
  is immediately served by the **hot path** — verified (`CreatedStub_IsServedByTheEngine_AndCounted`).
- **Stub id / metadata.** The adapter now honours an explicit `id`/`uuid` (else mints one) and parses
  the arbitrary `metadata` object onto `StubMapping.Metadata` — verified round-tripping
  `metadata.team`.
- **Regression cases:** `Mockifyr.Application.Tests.AdminCqrsTests` (5 cases).

## G7b — admin HTTP facade, validated semantically against the oracle

- **Group / item:** G7b. `Mockifyr.Facade.Admin` maps the WireMock-compatible `/__admin/*` routes to
  Mediant commands/queries (`AdminEndpoints.MapAdminEndpoints`); `Mockifyr.Server`'s host wires
  `AddMockifyr` + the endpoints. Thin: HTTP → `ISender.Send` → Application → Core.
- **How it's validated.** The **same** admin scenario is driven over HTTP against both the oracle and
  Mockifyr's in-memory admin host (`WebApplicationFactory<Program>`), and the **observation sequence**
  — status codes + mapping counts — must match. Per-engine stub ids differ, so the comparison is
  semantic (effects), not byte-for-byte. Verified identical on both:
  `reset→200`, `count 0`, `create→201`, `count 1`, `get→200`, `getMissing→404`, `delete→200`,
  `count 0`, `import(bundle of 2)→200`, `count 2`, `malformed create→422`, `reset→200`, `count 0`.
- **Status codes matched to the oracle:** create **201**, get/delete/import/reset/list **200**, a
  missing id **404**, malformed stub JSON **422** (the handler catches the parse error and returns a
  validation `Result`, which the endpoint maps to 422 — no exceptions for control flow).
- **Deferred:** mock-serving over HTTP (a catch-all → engine → wire response) belongs to the
  transport facade (**G12**); `/__admin/scenarios*` listing and the rich admin response JSON export
  shape (only ids/counts are surfaced now); tenant resolution (default tenant until G12).
- **Regression case:** `G7bAdminHttpTests.Admin_Crud_MatchesTheOracle`.

> **G7 is complete** with G7b: the management path is a full Mediant CQRS layer behind a
> WireMock-compatible admin HTTP surface, validated in-process (handlers) and over HTTP (semantic
> differential).
