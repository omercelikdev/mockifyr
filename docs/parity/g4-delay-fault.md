# Parity notes — G4 Delay + fault injection

Verified WireMock delay/fault behaviors against the oracle (`wiremock/wiremock:3.10.0`). See
[README](README.md) for the format. These are **transport/timing** behaviors: the pure engine only
records a directive (`CanonicalResponse.Delay`/`.Fault`); a facade applies it.

## Response delay (G4)

- **Group / item:** G4 — validated against the oracle.
- **`fixedDelayMilliseconds`** delays the response by that many ms; the response **content is
  unchanged** (verified: `fixedDelayMilliseconds: 600` → the body/status/headers are identical to an
  undelayed stub, just ~600ms later).
- **How it's validated.** The delayed response's content still diffs green, **and** both sides take
  at least the requested delay. Only a **generous lower bound** is asserted (delay 400ms → both
  ≥ 300ms): a fixed delay can never make a response *faster*, so this is robust against CI timing
  noise while still catching a delay that isn't applied. Timing is measured by
  `DifferentialRunner.ProbeTimedAsync`.
- **Where it's applied.** The engine stays pure — it only puts a `DelayDirective` on the response.
  The **library facade** (`MockifyrServer.Handle`) applies the delay in-process (the HTTP facade will
  apply it over the wire at G12). See docs/decisions/0001.
- **Deferred:** `delayDistribution` (lognormal/uniform random delays) and `chunkedDribbleDelay`.
- **Regression cases:** `G4DelayTests.FixedDelay_ContentParityAndTiming`,
  `G4DirectiveParsingTests.FixedDelay_IsParsedOntoTheResponse`.

## Fault injection (G4 — parsed; emission deferred to G12)

- **`fault`** is a **socket-level** behavior, so it **cannot be diffed through the in-process
  harness** (which drives the engine, not a socket). Probed against the oracle over HTTP:
  - `EMPTY_RESPONSE` → the connection is closed with no response (`curl` sees HTTP 000).
  - `MALFORMED_RESPONSE_CHUNK` → a 200 status line followed by garbage, then close.
  - `RANDOM_DATA_THEN_CLOSE` → random bytes, then close.
  - `CONNECTION_RESET_BY_PEER` → the connection is reset.
- **What G4 does now.** The adapter parses `fault` into a `FaultDirective(FaultKind)` on the response
  (all four kinds), so the directive is recorded and unit-tested. **Emitting the socket behavior and
  validating it belong to the HTTP facade (G12)** — there is no transport to produce or observe it
  in-process yet.
- **Regression case:** `G4DirectiveParsingTests.Fault_IsParsedOntoTheResponse` (all four kinds).
