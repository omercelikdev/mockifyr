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

### Advanced URL matchers (G1b)

- **Group / item:** G1b — fuzz-validated against the oracle.
- **`urlPattern` (urlMatching)** is a regex over the **full URL** (path + query) and is **anchored**
  (Java `matches`): `/things/[0-9]+\?ok=true` matched `/things/12?ok=true` but not `/things/12`.
- **`urlPathPattern` (urlPathMatching)** is a regex over the **path only** (query ignored) and is
  anchored to the whole path: `/u/[a-z]+` matched `/u/abc?x=1` but not `/u/abc/def`.
- **`urlPathTemplate`** matches the path structurally — each `{var}` consumes exactly one non-empty
  segment, the query is ignored, and both a missing and an extra segment fail. We compile the
  template to an anchored `[^/]+`-per-variable regex.
- **Named path-variable extraction** (exposing `{id}` to response templating) is **deferred to G2b**;
  only the match decision is observable through the oracle here.
- **Regression cases:** `G1GeneratedMatcherTests.Url_{Pattern,PathPattern,PathTemplate}`
  (differential), `MatcherTests.Url*` (pure logic).

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
- **`binaryEqualTo` verified.** An exact byte-for-byte body comparison against the base64-decoded
  expected value, correct for non-text payloads (validated with raw bytes `00 01 FF 10`). Handled by
  a dedicated `BinaryEqualToBodyMatcher` (bytes, not the text value-matcher path);
  `G1GeneratedMatcherTests.Body_BinaryEqualTo`.
- **Not yet validated against the oracle:** cookie **value** matching (see below), multi-value
  header/query (`havingExactly`/`including`).

### `equalToIgnoreCase` is `equalTo` + `caseInsensitive: true`

- **Group / item:** G1c/G1d — **found by the fuzzing generator**.
- **Behavior:** WireMock JSON has **no** `equalToIgnoreCase` key; a stub using it does not match
  (the oracle returned 404 for every case-variant). Case-insensitive equality is
  `{ "equalTo": "X", "caseInsensitive": true }`, and it works on header, query, **and body**.
- **Our handling:** the adapter maps `caseInsensitive: true` to a case-insensitive
  `EqualToValueMatcher`; the standalone `equalToIgnoreCase` key was removed for parity.
- **Regression cases:** `G1GeneratedMatcherTests.EqualToIgnoreCase_{Header,Body}`.

### Multi-value matchers hasExactly / includes (G1c)

- **Group / item:** G1c — fuzz-validated on a **query parameter**.
- **Real key names.** The keys are `hasExactly` and `includes` — **not** the roadmap's
  "havingExactly/including", which the oracle rejects with `422`.
- **`hasExactly`** requires the values to correspond **exactly** to the matcher list, in any order:
  `p=a&p=b` and `p=b&p=a` matched `[equalTo a, equalTo b]`, but `p=a` (missing) and `p=a&p=b&p=c`
  (extra) did not.
- **`includes`** is an order-insensitive **subset**: every matcher must match some value, extra
  values are allowed (`p=a&p=b&p=c` matched `[equalTo a, equalTo b]`).
- **Validated on query, not headers.** The differential harness's HTTP client folds repeated request
  **headers** into a single comma-joined value, so a multi-value *header* can't be exercised
  faithfully yet; query parameters carry multi-value unambiguously. The matchers
  (`HasExactlyValueMatcher`/`IncludesValueMatcher`) operate on any multi-valued target, so header
  multi-value follows once the harness/facade sends discrete header lines.
- **Regression cases:** `G1GeneratedMatcherTests.MultiValue_{HasExactly,Includes}` (differential),
  `ValueMatcherTests.{HasExactly,Includes}_*` (pure logic).

### Cookie value matching (G1c) — resolved; the divergence was a harness artifact

- **Group / item:** G1c (cookies) — originally deferred as a suspected WireMock case-normalization,
  now **root-caused and validated**.
- **Real cause: HTTP keep-alive connection reuse in the oracle client.** The differential harness
  reused one `HttpClient` across every probe. On a **reused** keep-alive connection the oracle
  received a **lowercased** Cookie header (`probe=A` → WireMock saw `a`; `preApost` → `preapost`),
  and a `"` in a value made the servlet drop the cookie entirely. A **fresh** connection transmits
  the header verbatim. Confirmed three ways: curl and a standalone `HttpClient` both send verbatim;
  the oracle's own near-miss diagnostic showed the lowercased value; and forcing a new connection
  fixed it. **Mockifyr's `CookieMatcher` was correct the whole time.**
- **Fix:** `WireMockOracle.SendAsync` sets `request.Headers.ConnectionClose = true` so each probe
  uses a fresh connection. Cookie `equalTo`/`contains` now diff green against the oracle
  (`G1GeneratedMatcherTests.{EqualTo,Contains}_Cookie`).
- **Corpus note.** A `"` (and other separators) in a cookie **value** is invalid per RFC 6265 and the
  servlet drops such a cookie, so cookie fuzzing is restricted to the cookie-safe character subset
  (letters/digits/`-._`), matching what a real client could send.

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

### clientIp (G1k) — NOT in open-source WireMock (no oracle)

- **Group / item:** G1k — **investigated against the oracle; no counterpart.**
- **Finding:** a request-level `clientIp` matcher is **rejected** by WireMock 3.10.0 with
  `422 Unrecognized field "clientIp" (class ...RequestPattern)`. It is not part of the open-source
  mapping DSL (and would be non-deterministic to diff, since the client address is the harness
  container's). Like the standalone number matchers, there is no oracle for it — **left unchecked**;
  revisit only if a maintainer wants it as a non-parity extension.

### multipartPatterns (G1k)

- **Group / item:** G1k — fuzz-validated against the oracle.
- **Semantics verified (WireMock 3.10.0):**
  - **`matchingType` defaults to `ANY`.** With no explicit type, a stub matched when *any* part
    satisfied the pattern; `ALL` requires *every* part to satisfy it.
  - **Body patterns are same-part AND.** A part *satisfies* a pattern only when **all** of the
    pattern's `bodyPatterns` match **that same part** — `[contains a, contains b]` matched a part
    `"ab"` but not two parts `"a"` and `"b"`.
  - **`name` is a no-op.** A pattern `name` that matches no part still matched under `ANY`, and a
    non-matching-`name` part still counted — so the oracle ignores `name` entirely. We ignore it too
    for parity (rather than replicate an intuitive-but-absent filter).
  - **A non-multipart request never matches** a `multipartPatterns` stub (verified with a
    `text/plain` body → 404).
- **Our handling:** `MultipartBodyParser` (pure, in Core) splits the body into parts when building
  the canonical request; `MultipartMatcher` applies the ANY/ALL logic over `part.Body`.
- **Deferred:** per-part `headers` matchers, binary (non-UTF-8) parts, quoted/edge boundaries, and
  whether multiple `multipartPatterns` entries AND together (assumed, lightly covered).
- **Regression cases:** `G1GeneratedMatcherTests.Multipart` (differential),
  `MultipartBodyParserTests` (pure logic).

### stub priority & selection (G1k)

- **Group / item:** G1k — fuzz-validated with multi-stub mappings loaded via
  `/__admin/mappings/import`.
- **Verified against the oracle:** when several stubs match the same request, the one with the
  **lowest `priority` number wins**, and an **unset priority defaults to 5** (an explicit `3` beats
  an unset stub; an explicit `8` loses to one). `StubEngine` already ordered by
  `priority` ascending, and the adapter's default of `5` is correct.
- **Equal-priority tie-break is load-path dependent — deferred.** WireMock resolves ties by
  insertion order, but the direction depends on how the stub was added: via `/__admin/mappings/import`
  the **earlier array element** wins (append/preserve order), whereas via individual
  `POST /__admin/mappings` the **most recently posted** wins (prepend). Because Mockifyr's own
  stub-add path (admin API) arrives at G7, the tie-break is validated then; selection scenarios here
  use **distinct** priorities so the outcome is order-independent.
- **Regression case:** `G1GeneratedMatcherTests.Selection_Priority`.

### logical matchers and / or / not (G1k)

- **Group / item:** G1k — fuzz-validated on a query parameter.
- **Shapes verified against the oracle:** `and` and `or` take an **array** of content patterns that
  apply to the same target value; `not` takes a **single** matcher object. `and` requires all, `or`
  requires at least one, `not` negates. They nest (`not(or(...))` verified).
- **`not` matches an absent target — verified.** For `{ "not": { "equalTo": "x" } }` on a query
  parameter that is not sent, the oracle matches (200): the inner matcher fails on the absent value,
  so `not` succeeds. Our combinators pass the same `present`/`values` down, so `NotValueMatcher`
  reproduces this.
- **Regression cases:** `G1GeneratedMatcherTests.Logic_AndOrNot` (differential),
  `ValueMatcherTests.{And,Or,Not}_*` (pure logic).

### basicAuthCredentials (G1k)

- **Group / item:** G1k — fuzz-validated against the oracle.
- **Behavior verified:** `request.basicAuthCredentials: { username, password }` matches when the
  request carries `Authorization: Basic <base64(username:":"password)>` **exactly**. A wrong
  username or password, a non-base64 token, or a missing `Authorization` header → no match on both
  sides. It is pure sugar for a header equal-to matcher, so it composes with any other header/query
  constraint on the stub.
- **Our handling:** the import adapter desugars it to a `HeaderMatcher("Authorization", equalTo
  token)` appended to the request's header matchers.
- **Regression case:** `G1GeneratedMatcherTests.BasicAuth`.

### matchesJsonSchema (G1h)

- **Group / item:** G1h — fuzz-validated over the common JSON Schema subset.
- **Library:** WireMock uses `networknt/json-schema-validator`; we use **json-everything's
  JsonSchema.Net** (MIT), confined to `Mockifyr.Matching`. Default dialect is **Draft 2020-12**,
  matching WireMock's default.
- **JSON shape verified against the oracle:** `matchesJsonSchema` accepts the schema as **either** an
  inline JSON object/array **or** an escaped JSON string, with an optional sibling `schemaVersion`.
  Both forms were loaded and diffed green.
- **Validation behaviour verified:** the two validators agree across `type`, `required`,
  `properties`, numeric bounds (`minimum`/`maximum`, inclusive), `enum`, and array `items`/`minItems`.
  A body that fails validation, is missing a required field, has a wrong-typed value, or is not JSON
  at all → no match (404 on both sides). `additionalProperties` is allowed by default (extra fields
  match).
- **Dialect selection.** A schema declaring `$schema` self-selects its draft; when a `schemaVersion`
  is supplied and the schema omits `$schema`, we inject the corresponding meta-schema id
  (`V6`→Draft 6, `V7`→Draft 7, `V201909`→2019-09, `V202012`→2020-12).
- **Deferred:** WireMock's `V4` (Draft 4 — unsupported by JsonSchema.Net), `format` assertion
  differences, `$ref`/remote-ref resolution, and draft-specific keyword edges beyond the common
  subset above.
- **Regression cases:** `G1GeneratedMatcherTests.MatchesJsonSchema_{InlineObject,StringFormAndVersion}`
  (differential), `MatchesJsonSchemaTests` (pure logic).

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
