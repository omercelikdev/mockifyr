# Parity notes — G1 Matching

Verified WireMock behaviors discovered while building the matching vertical against the oracle
(`wiremock/wiremock:3.10.0`). See [README](README.md) for the format.

### Exact URL + method match serves the stub verbatim

- **Group / item:** G1a (urlEqualTo) / G2a (static response)
- **Input:** stub `{"request":{"method":"GET","url":"/hello"},"response":{"status":200,"body":"world"}}`, request `GET /hello`.
- **WireMock behavior:** responds `200` with body exactly `world`. No `Content-Type` is added when the stub does not declare one.
- **Our handling:** `UrlEqualToMatcher` + `MethodMatcher` select the stub; `StaticResponseRenderer` returns the body verbatim.
- **Regression case:** `G0TrivialStubTests.ExactUrl_StaticResponse_MatchesOracle`.

### Unmatched request returns 404

- **Group / item:** G1a (selection)
- **Input:** same stub, request `GET /nope`.
- **WireMock behavior:** returns `404`. (WireMock also serves a descriptive no-match body; that body is a moving target and is compared only from G6, so for now only the status is asserted.)
- **Our handling:** `StubResolution.Matched == false` maps to a bare `404`.
- **Regression case:** `G0TrivialStubTests.UnknownPath_YieldsNotFoundOnBothSides`.

### Standard matchers on headers / query / body

- **Group / item:** G1c (header/query), G1d (body)
- **Verified against the oracle:** `equalTo` (header, query, body), `absent` (header), `contains`
  (body), `matches` (body). A present-and-matching target is served; a wrong value or a failing
  matcher yields 404 on both sides.
- **`matches` is a full match.** WireMock uses Java `String.matches` semantics — the whole value
  must match. `matches: "[a-z]+[0-9]+"` matched `abc123` but not `abc123x` on the oracle; we
  reproduce this by anchoring the pattern with `\A(?:...)\z` in `MatchesValueMatcher`.
- **Regression cases:** `G1StandardMatcherTests` (8 cases).
- **Not yet validated against the oracle:** cookie matchers, `doesNotMatch`, `equalToIgnoreCase`,
  multi-value header/query (`havingExactly`/`including`), `binaryEqualTo`. Implemented but pending
  a differential case.

### Header masking (current harness limitation)

- WireMock injects transport/server headers (`Matched-Stub-Id`, `Vary`, `Transfer-Encoding`,
  `Server`, ...) that Mockifyr does not emit in-process. The differ therefore compares only the
  headers a stub explicitly declares; the masked set narrows as G2/G12 implement real
  header/wire behavior. Documented in `ResponseDiffer`.
