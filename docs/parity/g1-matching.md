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
- **Also fuzz-validated:** `doesNotMatch` (header/body); case-insensitive equality (header/body).
- **Not yet validated against the oracle:** cookie **value** matching (see below), multi-value
  header/query (`havingExactly`/`including`), `binaryEqualTo`.

### `equalToIgnoreCase` is `equalTo` + `caseInsensitive: true`

- **Group / item:** G1c/G1d — **found by the fuzzing generator**.
- **Behavior:** WireMock JSON has **no** `equalToIgnoreCase` key; a stub using it does not match
  (the oracle returned 404 for every case-variant). Case-insensitive equality is
  `{ "equalTo": "X", "caseInsensitive": true }`, and it works on header, query, **and body**.
- **Our handling:** the adapter maps `caseInsensitive: true` to a case-insensitive
  `EqualToValueMatcher`; the standalone `equalToIgnoreCase` key was removed for parity.
- **Regression cases:** `G1GeneratedMatcherTests.EqualToIgnoreCase_{Header,Body}`.

### Cookie value matching diverges (deferred)

- **Group / item:** G1c (cookies) — **found by the fuzzing generator**.
- **Observation:** cookie **presence** (`absent`) matches the oracle, but cookie **value**
  matching (`equalTo`/`contains`) diverges in a way consistent with WireMock normalizing cookie
  value case. Needs a focused investigation (WireMock cookie parsing + client transport).
- **Status:** `CookieMatcher` and cookie parsing are implemented and unit-tested
  (`ValueMatcherTests.CookieMatcher_*`); only `Absent_Cookie` is validated against the oracle.

### Empty request body is "absent" for body matching

- **Group / item:** G1d (body) — **found by the fuzzing generator**, not by hand.
- **Input:** stub `bodyPatterns: [{ "equalTo": "" }]`, request with an empty body.
- **WireMock behavior:** does **not** match (404). WireMock treats an empty request body as absent
  for body-pattern evaluation, so `equalTo ""` fails against it.
- **Our handling:** `BodyMatcher` reports the target as absent when the body is empty, so the
  value matcher sees `present: false`.
- **Regression case:** `G1GeneratedMatcherTests.EqualTo_Body` (corpus value `""`).

### Corpus exclusions (transport ambiguity, not yet validated)

Header/query fuzzing currently skips empty and whitespace-only values and non-ASCII/control
characters, which carry transport-level encoding/trimming ambiguity to be validated separately.
Tracked in `TextCorpus`.

### Header masking (current harness limitation)

- WireMock injects transport/server headers (`Matched-Stub-Id`, `Vary`, `Transfer-Encoding`,
  `Server`, ...) that Mockifyr does not emit in-process. The differ therefore compares only the
  headers a stub explicitly declares; the masked set narrows as G2/G12 implement real
  header/wire behavior. Documented in `ResponseDiffer`.
