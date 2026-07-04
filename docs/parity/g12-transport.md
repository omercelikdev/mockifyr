# Parity notes — G12 Transport HTTP facade

Verified WireMock behaviors that are only observable **over the wire**, against the oracle
(`wiremock/wiremock:3.10.0`). The mock-serving HTTP facade is where the several transport/socket
behaviors deferred throughout the roadmap are finally implemented and validated.

## Mock serving over HTTP (G12a)

- **Group / item:** G12a — validated **over real HTTP** (not in-process). `Mockifyr.Facade.Http`'s
  `MapMockServing` fallback turns every non-admin request into a `CanonicalRequest`, resolves it
  through the pure `StubEngine`, and writes the response to the wire. `Mockifyr.Server` hosts the
  admin surface + this fallback.
- **How it's validated.** The same stub is loaded and driven over HTTP against **both** the oracle and
  a hosted Mockifyr (`WebApplicationFactory<Program>`), and the wire responses are compared — one
  `DriveOverWire(client, …)` helper run on each (like the G7b admin diff). Reason phrase, multi-value
  headers, and the body are all read from the real `HttpResponseMessage`.
- **Closed here (previously deferred to G12):**
  - **Mock serving over HTTP** — responses are now produced by a real transport, not just in-process.
  - **`statusMessage`** — the custom **reason phrase** is written on the status line (`HTTP/1.1 418 I
    am a teapot`) via `IHttpResponseFeature.ReasonPhrase`, and diffed. (It was parsed since G2a but
    only assertable over the wire.)
  - **Multi-value response headers on the wire** (`X-Multi: a` / `X-Multi: b`) and `jsonBody` served
    over HTTP.
- **Response `delay`** is applied by the facade (`await Task.Delay`) before writing — the in-process
  library facade already did this; the HTTP facade now does it over the wire.
- **Server-injected headers are masked.** The oracle adds `Matched-Stub-Id`, `Transfer-Encoding:
  chunked`, etc.; only the headers a scenario declares are compared (plus status, reason phrase, body).
- **Tenant resolution.** The facade reads an optional `X-Mockifyr-Tenant` header, else the default
  tenant (the oracle is single-tenant, so the wire diff uses the default).
- **Deferred within G12:** socket **fault emission** (the four `fault` kinds) → **G12b**;
  `delayDistribution` → G12b; `/__admin/scenarios*`, `/__admin/recordings/*`, `/__admin/ext/*`, and
  gzip/`Content-Encoding` → **G12c**; TLS + HTTP/2 → **G11**. The **no-match 404 body** is WireMock's
  verbose near-miss diagnostic table — a genuine moving target tied to G6; only the 404 **status** is
  served/diffed, the body is left for a focused near-miss-diagnostic follow-up.
- **Regression case:** `G12aHttpServingTests.Serves_OverTheWire_MatchingTheOracle`.
