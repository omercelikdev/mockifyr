# Parity notes — G9 Record & Playback

Verified WireMock recorder behaviors against the oracle (`wiremock/wiremock:3.10.0`). See
[README](README.md) for the format.

## Record → playback (G9)

- **Group / item:** G9 — validated against the oracle by **cross-engine replay**.
- **Shape.** WireMock's recorder (`POST /__admin/recordings/start {targetBaseUrl}` … `/stop`, or
  `/snapshot`) proxies requests to an upstream, captures the request/response pairs, and **generates
  stub mappings** that replay each response. The generated stub uses an exact-URL + method request
  pattern (plus an `equalTo` body pattern for a request with a body) and the captured status/body/
  headers as a static response (verified: `bodyPatterns: [{equalTo, caseInsensitive:false}]` for a
  `POST`, no body pattern for a `GET`; the response carries the upstream's headers).
- **Why cross-engine replay, not a JSON diff.** The generated mapping JSON is a moving target — ids,
  the derived `name`, volatile `Date`/`Server` headers, `persistent`, and body-pattern choices. So
  instead of byte-diffing the generated JSON: Mockifyr **records** (proxies + captures + generates a
  stub), then the generated stubs are loaded into **both** the real oracle and a fresh Mockifyr and
  the requests are **replayed**. The claim: *a stub Mockifyr recorded is WireMock-valid and replays
  the captured response on the real WireMock*.
- **What's validated.** For each recorded request: the oracle's replay of Mockifyr's generated stub
  equals the captured upstream response, and Mockifyr's own replay equals the oracle's — compared on
  **status + body + stable headers** (`X-Upstream`, `Content-Type`). Transport/volatile headers
  (`Content-Length`, `Transfer-Encoding`, `Connection`, `Date`, `Server`) are **not baked into the
  generated stub** (the serving side recomputes them) and are masked in the diff.
- **Architecture.** `StubRecorder` (`Mockifyr.Facade.Library`) reuses `ProxyResponder` for the
  outbound call and `RecordingJsonWriter` (`Mockifyr.Adapters.MappingJson`) to generate the stub
  JSON from the captured exchange — the inverse of the import adapter, scoped to recorded stubs (no
  general model→JSON export needed).
- **Deferred:** the `/__admin/recordings/*` and `/snapshot` **admin endpoints** (the recorder is
  driven in-process for validation; wiring it behind HTTP is a small follow-up with the other admin
  routes); record `filters`/`allowNonProxied`; body-file (`__files`) extraction; response
  `transformers`; and repeat-request → **scenario** generation.
- **Regression case:** `G9RecordPlaybackTests.Recorded_Stubs_ReplayTheCapturedResponse`.
