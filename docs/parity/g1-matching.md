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

### equalToJson (G1e)

- **Group / item:** G1e — fuzz-validated across all four `ignoreArrayOrder` × `ignoreExtraElements`
  combinations.
- **Verified against the oracle:** key order and whitespace are irrelevant; numbers compare by
  value (`1` == `1.0`); types are significant (`1` != `"1"`); strict mode rejects extra fields and
  reordered arrays; `ignoreExtraElements` allows extra object fields (but not missing ones);
  `ignoreArrayOrder` treats arrays as multisets. Our `EqualToJsonValueMatcher` agreed with the
  oracle on every generated case (`G1GeneratedMatcherTests.EqualToJson_*`).
- **Edge behaviors verified (`EqualToJson_Edges`):** number precision (`1.0` == `1.00` == `1e2`);
  explicit `null` is distinct from a missing key and from `"null"`; objects nested in arrays
  reorder under `ignoreArrayOrder`.
- **`ignoreExtraElements` allows extra *array* items too — found by the generator.** For an ordered
  array, WireMock matched `[1,2]` against `[1,2,3]` (200), i.e. the expected array must match a
  **prefix** and extra trailing items are ignored; reordering is still rejected (`[2,1]` no-match).
  Our `EqualToJsonValueMatcher` was fixed to match this (was requiring equal array length).
- **Still not covered:** duplicate keys (undefined `System.Text.Json` behavior) and equalToJson on
  non-body targets.

### matchesJsonPath (G1f)

- **Group / item:** G1f — fuzz-validated across the common JSONPath subset.
- **Library:** WireMock uses Jayway JsonPath; we use **Newtonsoft.Json**'s JSONPath, the closest
  .NET proxy (it accepts Jayway-style syntax; `JsonPath.Net`'s RFC-9535 dialect would reject
  `[?(@.x=='y')]`). Confined to `Mockifyr.Matching`.
- **Verified against the oracle:** the presence form (property access `$.a.b`, array index
  `$.items[0]`, wildcard `$.store.book[*].author`, recursive descent `$..id`) matches when the
  path selects ≥1 node; the expression + sub-matcher form (`{ "expression": "...", "equalTo": ...}`)
  applies the sub-matcher to the extracted value (a number `30` extracts as `"30"`). Invalid body
  or invalid path expression → no match.
- **Not yet validated (deferred, likely Jayway-vs-Newtonsoft divergence):** filter expressions
  `[?(@.price > 10)]`, functions (`.length()`), and multi-value sub-matcher semantics on
  indefinite paths.

### Header masking (current harness limitation)

- WireMock injects transport/server headers (`Matched-Stub-Id`, `Vary`, `Transfer-Encoding`,
  `Server`, ...) that Mockifyr does not emit in-process. The differ therefore compares only the
  headers a stub explicitly declares; the masked set narrows as G2/G12 implement real
  header/wire behavior. Documented in `ResponseDiffer`.
