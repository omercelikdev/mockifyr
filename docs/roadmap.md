# Mockifyr — Roadmap

Derived from the WireMock feature inventory: ~40 validated steps. **The gate for every
step:** green oracle diff + the regression suite grows + a commit + a short summary. At every
checkpoint: stop, show, get approval. No autonomous drift.

Detailed rationale and per-group contents:
[../ARCHITECTURE.md](../ARCHITECTURE.md#12-full-parity-roadmap-40-validated-steps).

## Phase A — Narrow vertical (first working, proven core)

- [x] **G0** — Foundation + differential harness (solution layout, engine interfaces, tenant
  model, in-memory store, Java WireMock container + canonical diff). Gate met: the harness
  diffs a trivial stub (exact URL + static response) against the `wiremock/wiremock:3.10.0`
  oracle, green. Also lands the first slice of G1a (urlEqualTo/urlPathEqualTo/method/ANY) and
  G2a (static response). The generator is still a stub.
- [ ] **G1 — Matching**
  - [ ] G1a URL basic (urlEqualTo, urlPathEqualTo, method + ANY)
  - [x] G1b URL advanced — `urlPattern` (anchored full-URL regex), `urlPathPattern` (anchored
    path regex), `urlPathTemplate` (one segment per `{var}`), fuzz-validated. Named path-variable
    **extraction** is deferred to G2b (only the match decision is observable now)
  - [ ] G1c header/query/cookie matchers (+ multi-value) — header/query `equalTo`/`contains`/
    `absent`/`doesNotMatch`/`caseInsensitive` fuzz-validated; cookie **value** matching and
    multi-value (`havingExactly`/`including`) still pending
  - [ ] G1d body basic (equalTo, binaryEqualTo, contains, matches) — `equalTo`/`contains`/
    `matches`/`doesNotMatch`/`caseInsensitive` fuzz-validated; `binaryEqualTo` pending
  - [x] **Fuzzing generator** (brief §5) — deterministic seed-driven `MatcherScenarios` emit
    hundreds of corpus-spanning probes; the property suite asserts the match decision agrees
    with the oracle. It already caught the empty-body divergence above.
  - [x] G1e equalToJson (ignoreArrayOrder × ignoreExtraElements) — semantic JSON comparator
    fuzz-validated across all 4 flag combinations plus edges (number precision, null,
    nested-in-array reorder, extra trailing array items). Only duplicate keys and non-body
    targets remain unfuzzed
  - [x] G1f matchesJsonPath — presence + expression/sub-matcher forms fuzz-validated over the
    common subset (property/index/wildcard/recursive-descent) via Newtonsoft as the Jayway proxy;
    filters `[?(...)]`, functions, and indefinite-path sub-matchers deferred
  - [x] G1g equalToXml / matchesXPath — semantic XML equality (whitespace/attr-order/**sibling
    order** insensitive) and XPath presence + text/attribute sub-matcher, fuzz-validated via
    System.Xml; placeholders, namespaceAwareness, namespaced XPath, functions, element-node
    sub-matcher deferred
  - [x] G1h matchesJsonSchema — JSON Schema validation via json-everything's JsonSchema.Net
    (default Draft 2020-12); inline + string schema forms and `schemaVersion` fuzz-validated over the
    common keyword subset (type/required/properties/bounds/enum/items). Draft 4, `format` assertions,
    and `$ref` resolution deferred
  - [x] G1i date/time matchers — `before`/`after`/`equalToDateTime` on absolute ISO-8601 instants
    (+ `actualFormat`) fuzz-validated; `now`-relative/offset/truncation deferred (racy vs a second
    clock)
  - [x] G1j number matchers — delivered as **JSONPath numeric filters** (`[?(@.x > n)]`),
    fuzz-validated against the oracle for `>`/`>=`/`<`/`<=`/`==` on int & decimal. The standalone
    `equalToNumber`/`greaterThanNumber`/… keys are **not in open-source WireMock** (Cloud-only, no
    oracle) — see docs/parity/g1-matching.md
  - [x] G1k logic (`and`/`or`/`not`) + basicAuth + multipart + stub priority/selection, each
    fuzz-validated. **clientIp is not in open-source WireMock** (rejected `422`, no oracle) — deferred
    like the standalone number matchers. The equal-priority tie-break (load-path dependent) and
    per-part multipart headers are deferred; see docs/parity/g1-matching.md
- [ ] **G2 — Response + templating**
  - [ ] G2a static response (+ bodyFileName templating, gzip)
  - [ ] G2b templating engine (Handlebars.Net + request model + named path vars)
  - [ ] G2c data helpers (jsonPath, xPath, regexExtract, formData, parseJson)
  - [ ] G2d date helpers
  - [ ] G2e random helpers
  - [ ] G2f json manipulation helpers
  - [ ] G2g format/math/array helpers
  - [ ] G2h system helpers
- [ ] **G3 — Webhook / correlation**
  - [ ] G3a serve-event listener + async outbound
  - [ ] G3b templated webhook + originalRequest correlation + sub-events

## Phase B — Everything else, up to parity

- [ ] **G4** Delay + fault injection
- [ ] **G5** Stateful scenarios
- [ ] **G6** Verify + near-miss diagnostics
- [ ] **G7** Admin API (full) + first-class stub metadata
- [ ] **G8** Proxying
- [ ] **G9** Record & Playback
- [ ] **G10** Extensibility (public — 7 extension types)
- [ ] **G11** HTTPS/TLS + HTTP/2
- [ ] **G12** Standalone/deploy + config
- [ ] **G13** gRPC extension
- [ ] **G14** GraphQL extension
- [ ] **G15** Message-based/WebSocket + JWT + Faker + multi-domain
- [ ] **G16** Persistence providers (FileBased/LiteDB/Postgres/Redis) + change-feed reload

## Post-phase (not now — architecture is ready for it)

- [ ] UI / dashboard (dark mode, design system, omercelik.dev brand language)
