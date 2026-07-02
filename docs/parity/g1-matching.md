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
- **Numeric filter expressions `[?(@.field <op> n)]` verified (G1j).** WireMock's Jayway engine and
  Newtonsoft agree across `>`, `>=`, `<`, `<=`, and `==` on integer and decimal fields: the filter
  selects the array elements passing the comparison, and the stub matches when ≥ 1 element passes.
  Comparisons are strict where written (`> 10` rejects `10`) and numeric equality is scale-insensitive
  (`== 3` matches `3.0`). Validated in `G1GeneratedMatcherTests.MatchesJsonPath_NumericFilters`.
- **Still not validated (deferred, likely Jayway-vs-Newtonsoft divergence):** functions
  (`.length()`), type-coercion edges in filters (comparing a numeric filter against string values),
  and multi-value sub-matcher semantics on indefinite paths.

### equalToXml / matchesXPath (G1g)

- **Group / item:** G1g — fuzz-validated over the common XML subset (`System.Xml.Linq`, no
  external dependency).
- **equalToXml verified:** insignificant whitespace and attribute order are ignored; leaf text is
  whitespace-normalized; text and attribute *values* are significant.
- **Sibling element order is NOT significant — found by the generator.** WireMock matched
  `<order><qty/><item/></order>` against `<order><item/><qty/></order>` (200), i.e. XMLUnit treats a
  reorder as "similar". `EqualToXmlValueMatcher` now matches children as a multiset (was positional).
- **matchesXPath verified:** the presence form (element `/order/item`, descendant `//qty`, attribute
  `/order/@id`) matches when the expression selects ≥ 1 node.
- **Sub-matcher extraction needs the text node — found by the generator.** `/order/item` (element)
  did NOT equal `"book"` on the oracle; the text node `/order/item/text()` does. We extract the
  value of text nodes/attributes; **element-node sub-matcher extraction is deferred** (WireMock
  serializes the node differently).
- **Deferred:** placeholders, `exemptedComparisons`, `namespaceAwareness` modes, namespaced XPath,
  XPath functions, and mixed content.

### date/time matchers (G1i)

- **Group / item:** G1i — fuzz-validated over the deterministic subset (`System.DateTimeOffset`, no
  external dependency).
- **Keys verified against the oracle:** `before`, `after`, and `equalToDateTime` are all real
  WireMock JSON keys and were exercised on a query parameter. `before`/`after` are **strict**
  (a value equal to the expected instant does not satisfy either), and `equalToDateTime` requires
  the exact instant. An unparseable actual value → no match (404 on both sides).
- **Comparison is on the instant.** Our `DateTimeValueMatcher` reads both sides as
  `DateTimeOffset` in UTC (`AssumeUniversal | AdjustToUniversal`), so a value carrying an explicit
  offset compares by absolute instant. The corpus pins expected + actual in UTC (`Z`) to keep the
  diff unambiguous.
- **`actualFormat` verified.** Parsing the incoming value with a custom pattern (`dd/MM/yyyy`) and
  comparing against an ISO expected agreed with the oracle. We support the **overlapping** subset of
  Java `DateTimeFormatter` / .NET custom format patterns (e.g. `dd/MM/yyyy`, `yyyy-MM-dd`); patterns
  that differ between the two platforms (`X`/`Z` zone tokens, era/locale tokens) are not claimed.
- **Deferred (documented, not yet validated):** `now`-relative expected values (`"now +3 days"`) —
  WireMock evaluates `now` at request time, so diffing against a second clock is inherently racy;
  `expectedOffset`/`expectedOffsetUnit`, `truncateExpected`/`truncateActual`, and
  `applyTruncationLast`.
- **Regression cases:** `G1GeneratedMatcherTests.DateTime_{Comparisons,ActualFormat}` (differential),
  `ValueMatcherTests.DateTime_*` (pure logic).

### number matchers (G1j) — NOT in open-source WireMock (blocked, no oracle)

- **Group / item:** G1j — **investigated against the oracle; found to have no counterpart.**
- **Finding:** the numeric match operations named in the roadmap/docs (`equalToNumber`,
  `greaterThanNumber`, `greaterThanEqualNumber`, `lessThanNumber`, `lessThanEqualNumber`) are
  **rejected by every open-source WireMock version tested** — `3.10.0` (the pinned oracle),
  `3.12.1`, `3.13.1`, and `3.13.2` (latest). Loading a mapping returns
  `422 { code: 10, detail: "{...} is not a valid match operation" }`.
- **Verified across forms.** Rejected standalone on a query parameter, and as a `matchesJsonPath`
  sub-matcher, with the expected value given as both a JSON string (`"10"`) and a JSON number
  (`10`). None are accepted.
- **Where they actually live:** these keys appear only in **WireMock Cloud** documentation
  (docs.wiremock.io / mocklab.io), not the OSS engine that the differential harness runs. Numeric
  *comparison* in OSS WireMock is only reachable inside **JSONPath filter expressions**
  (`[?(@.price > 10)]`), which is the deferred part of G1f.
- **Consequence:** G1j cannot be built as a standalone matcher to this project's definition of done —
  there is no oracle to diff against, and golden rules #2/#3 forbid a self-validated matcher.
- **Resolution (maintainer decision):** G1j is delivered as **JSONPath numeric filters** — the
  oracle-validatable route to numeric matching in open-source WireMock. See the numeric-filter entry
  under *matchesJsonPath (G1f)* above; the standalone `*Number` keys remain unimplemented (Cloud-only).

### Header masking (current harness limitation)

- WireMock injects transport/server headers (`Matched-Stub-Id`, `Vary`, `Transfer-Encoding`,
  `Server`, ...) that Mockifyr does not emit in-process. The differ therefore compares only the
  headers a stub explicitly declares; the masked set narrows as G2/G12 implement real
  header/wire behavior. Documented in `ResponseDiffer`.
