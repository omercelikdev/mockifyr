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
- **Deferred to G7b:** the HTTP `/__admin/*` endpoints (`Mockifyr.Facade.Admin`) and their semantic
  differential validation via a test host; `/__admin/scenarios*` listing; the admin response JSON
  export shape.
- **Regression cases:** `Mockifyr.Application.Tests.AdminCqrsTests` (5 cases).
