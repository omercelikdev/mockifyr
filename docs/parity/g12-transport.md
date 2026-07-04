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

## Fault injection + delayDistribution (G12b)

- **Group / item:** G12b — validated **over a real Kestrel socket** (faults need genuine transport;
  the in-memory test server can't reproduce a connection reset). `MockifyrKestrelHost` starts Mockifyr
  on an ephemeral loopback port; the test drives it and the oracle with a real `HttpClient`.
- **All four `fault` kinds surface identically to an HTTP client** — a failed request. Verified: with
  a `.NET HttpClient`, `EMPTY_RESPONSE`, `MALFORMED_RESPONSE_CHUNK`, `RANDOM_DATA_THEN_CLOSE`, and
  `CONNECTION_RESET_BY_PEER` all throw `HttpRequestException` (inner `HttpIOException`) against the
  oracle — they are **not** distinguishable at the client layer. So the differential compares the
  **outcome class** (request failed vs succeeded): every fault stub must fail the request on both
  sides, and a control (non-fault) stub must succeed on both.
- **Emission.** The facade breaks the connection: empty-response/reset `context.Abort()` with nothing
  written; malformed/random write a few bytes first, then abort mid-response. Byte-level fidelity
  (garbage vs reset) is not client-observable and would need raw-transport access — deferred as a
  fidelity nuance, not a functional gap (the observable contract "the request fails" is met).
- **`delayDistribution` (uniform)** — a random delay drawn from `[lower, upper]`, applied by the
  facade. Validated over the wire with a **lower-bound** assertion (a uniform delay can't be shorter
  than `lower`; robust against CI variance). **Lognormal** distributions are deferred — no reliable
  lower bound to assert (racy, like `now`/random).
- **Regression cases:** `G12bFaultTests.Fault_BreaksTheConnection_LikeTheOracle` (4 kinds + control),
  `G12bFaultTests.UniformDelayDistribution_AppliesAtLeastTheLowerBound_LikeTheOracle`.
