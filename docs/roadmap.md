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
  - [x] G1c header/query/cookie matchers (+ multi-value) — header/query/**cookie**
    `equalTo`/`contains`/`absent`/`doesNotMatch`/`caseInsensitive` and multi-value
    `hasExactly`/`includes` (real keys, not `havingExactly`/`including`) fuzz-validated. Cookie value
    matching was root-caused (a harness keep-alive artifact, not a Mockifyr bug) and now diffs green.
    Header multi-value still awaits a harness that sends discrete header lines (query covers it)
  - [x] G1d body basic (equalTo, binaryEqualTo, contains, matches) — `equalTo`/`contains`/
    `matches`/`doesNotMatch`/`caseInsensitive` and now `binaryEqualTo` (exact byte comparison)
    fuzz-validated
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
- [x] **G2 — Response + templating** (G2a–G2h complete)
  - [x] G2a static response — status, multi-value headers, literal `body`, `jsonBody` (compact),
    `base64Body` (bytes) fuzz-validated. `statusMessage` parsed (not yet diffable); `bodyFileName`
    (needs `__files` + templating → G2b) and gzip (transport) deferred. See docs/parity/g2-response.md
  - [x] G2b templating engine — Handlebars.Net wired behind the `response-template` transformer;
    request model (`method`/`url`/`path`/`pathSegments`/`query`/`headers`/`body`), non-escaping
    output, and templated response headers fuzz-validated. `request.path.<name>` named path vars
    (dual string/object model) deferred to a focused follow-up; built-in helpers are G2c–G2h. See
    docs/parity/g2-response.md
  - [x] G2c data helpers — `jsonPath` (scalar, empty-on-miss, compact array, Jackson-pretty object),
    `xPath` (text/attr/string/count values + XML element serialization), `regexExtract`
    (whole-match, capture-group variable, `default=` / error string), `formData` (first value +
    `urlDecode`), `parseJson` (navigable variable), validated against the oracle. Multi-value
    `formData` indexing and the `parseJson` block form are deferred. See docs/parity/g2-response.md
  - [x] G2d date helpers — `parseDate` (ISO-8601 + Java `SimpleDateFormat` input) composed into
    `date` (Java format patterns incl. `E`/`a`/`S` translation, `epoch`/`unix`, default ISO, plural
    `offset=` units), validated against the oracle over fixed instants. `now`/now-relative and the
    unparseable-date fallback are racy and deferred; `timezone=` is ignored on a parsed instant to
    match the oracle. See docs/parity/g2-response.md
  - [x] G2e random helpers — `randomValue` (UUID + `[a-z0-9]`/`[a-z]`/`[0-9]`/`[0-9a-f]` types with
    `length`/`uppercase`), `pickRandom`, `randomInt` (half-open `[lower,upper)`), and bounded
    `randomDecimal`, validated **structurally** against the oracle (the racy output can't be
    byte-diffed, so the oracle and Mockifyr must each satisfy the same charset/length/range
    contract). `ALPHANUMERIC_AND_SYMBOLS` and unbounded-decimal distribution deferred. See
    docs/parity/g2-response.md
  - [x] G2f json manipulation helpers — `jsonArrayAdd` (parsed item + `maxItems` front-drop),
    `jsonMerge` (deep merge, B over A), `jsonRemove` (path delete) emit compact JSON; `toJson`
    emits Jackson-pretty (shared `JacksonJson.Write`, reused by `jsonPath`). Validated against the
    oracle. Array-valued key merge deferred. See docs/parity/g2-response.md
  - [x] G2g format/math/array helpers — jknack built-ins WireMock registers: `math` (`+ - * /`,
    half-up integer division, Java-style doubles), `numberFormat` (DecimalFormat pattern + currency/
    percent), `size`, `join`, `substring`, `replace`, `upper`, `lower`, `capitalize`, `trim`,
    validated against the oracle. `%`/`^` and non-OSS helpers (abs/round/split/…) deferred. See
    docs/parity/g2-response.md
  - [x] G2h system helpers — `systemValue` (deny-by-default `[ERROR: Access to <key> is denied]`,
    byte-diffed; permitted-key allowlist deferred to G12) and `hostname` (host-specific, validated
    structurally). `systemProperty`/`env` are not in open-source WireMock. See docs/parity/g2-response.md
- [x] **G3 — Webhook / correlation** (G3a–G3b; sub-event journaling → G6/G7)
  - [x] G3a serve-event listener + async outbound — `postServeActions` webhook (static
    method/url/headers/body) fired via `WebhookServeEventListener` (`IServeEventListener`), the
    engine's first outbound I/O at the facade edge. Validated differentially with a host-side
    webhook receiver (oracle reaches it via host.docker.internal). Templating/correlation → G3b.
    See docs/parity/g3-webhook.md
  - [x] G3b templated webhook + originalRequest correlation — the webhook `url` (path + query),
    header values, and body are Handlebars-templated against `originalRequest` (automatic, no
    transformer flag), reusing the response templating engine/helpers via the shared
    `HandlebarsFactory`/`RequestModel` and the `IServeEventTemplateRenderer` seam. Validated
    differentially. Sub-event recording deferred to G6/G7 (no admin/verify surface yet). See
    docs/parity/g3-webhook.md

## Phase B — Everything else, up to parity

- [x] **G4** Delay + fault injection — `fixedDelayMilliseconds` recorded as a `DelayDirective` and
  applied by the facade (content parity + robust lower-bound timing, both sides); `fault`
  (all four kinds) parsed into a `FaultDirective`. Socket-level fault *emission* and
  `delayDistribution` deferred to the HTTP facade (G12). See docs/parity/g4-delay-fault.md
- [x] **G5** Stateful scenarios — `scenarioName`/`requiredScenarioState`/`newScenarioState` parsed
  into `ScenarioBinding` (the engine already gated eligibility + wrote transitions); default start
  state `Started`. Validated differentially with a multi-step state walk and per-scenario isolation.
  Direct state-set + scenarios admin listing → G7. See docs/parity/g5-scenarios.md
- [x] **G6** Verify + near-miss diagnostics — `count`/`find`/`unmatched` over the request journal
  (reusing the stub matchers; `{}` matches all) validated **semantically** against the oracle's
  `/__admin/requests*` (counts, not the volatile-field-heavy JSON). Near-miss ranking by ascending
  match distance validated as pure logic. Cross-engine near-miss identity deferred. See
  docs/parity/g6-verify.md
- [x] **G7** Admin API (full) + first-class stub metadata
  - [x] G7a Application/CQRS + metadata — the `Mockifyr.Application` management path (Mediant 1.0.0):
    Create/Delete/Import/Reset stub commands + GetStubs/GetStub/CountRequests/FindUnmatched queries,
    `Result<T>` pattern, dispatched via `ISender`. `AddMockifyr` composes shared stores + engine +
    handlers, so the management path and serving hot path share state. Adapter now parses stub
    `id`/`uuid` and `metadata`. Validated in-process. See docs/parity/g7-admin.md
  - [x] G7b Admin HTTP facade — `Mockifyr.Facade.Admin` maps `/__admin/*` (mappings CRUD/import/reset,
    requests/count) to `ISender`; validated over HTTP via a `WebApplicationFactory` test host by
    comparing the status-code + mapping-count observation sequence to the oracle (201/200/404/422 all
    match). Mock-serving-over-HTTP + `/__admin/scenarios*` deferred to G12. See docs/parity/g7-admin.md
- [x] **G8** Proxying — `proxyBaseUrl` recorded as a `ProxyDirective`; a facade edge
  (`ProxyResponder`) forwards the matched request (method + path/query + body + headers) to the
  upstream and returns its response. Validated differentially: both sides proxy to one shared
  host-side upstream and the proxied response (status + body + marker header) matches.
  `additionalProxyRequestHeaders` / URL rewriting deferred. See docs/parity/g8-proxy.md
- [x] **G9** Record & Playback — `StubRecorder` proxies to the target, captures the exchange, and
  `WireMockRecordingWriter` generates a stub (exact URL + method + body `equalTo`, captured response).
  Validated by **cross-engine replay**: Mockifyr's generated stubs, loaded into the real oracle,
  replay the captured response (and Mockifyr replays them identically). Recorder admin endpoints,
  filters, body-file extraction, and scenario generation deferred. See docs/parity/g9-record-playback.md
- [x] **G10** Extensibility (public) — `AddMockifyr(cfg => …)` with a `MockifyrExtensions` builder
  registers user extensions; four types validated in-process (custom **matcher** via `customMatcher`,
  **serve-event listener**, **template helper**, **response transformer**). The Core seams were
  already public/dogfooded; the remaining ones (`IResponseDefinitionTransformer`,
  `ITemplateModelProvider`, `IAdminApiExtension`, `IMappingsLoader`) are wired incrementally.
  Validated in-process (not oracle-differential — custom extensions have no WireMock equivalent). See
  docs/parity/g10-extensibility.md
- [ ] **G11** HTTPS/TLS + HTTP/2
- [ ] **G12** Transport HTTP facade + standalone/deploy + config
  - [x] G12a Mock-serving HTTP facade — `Mockifyr.Facade.Http` fallback (request → engine → wire),
    hosted by `Mockifyr.Server`. Validated **over the wire** against the oracle (status, reason
    phrase/`statusMessage`, multi-value headers, body, `jsonBody`). Closes mock-serving-over-HTTP +
    `statusMessage`; `delay` applied by the facade; tenant via `X-Mockifyr-Tenant`/default. See
    docs/parity/g12-transport.md
  - [x] G12b Socket faults + `delayDistribution` — all four `fault` kinds emitted over a real Kestrel
    socket (they surface to an HTTP client identically, as a failed request; diffed as
    failed-vs-succeeded against the oracle) and uniform `delayDistribution` (lower-bound timing).
    Lognormal distribution and byte-level fault fidelity deferred. See docs/parity/g12-transport.md
  - [x] G12c Scenarios admin + gzip — `GET /__admin/scenarios` (state + `possibleStates`), set-state,
    reset; and gzip response encoding when the client accepts it. Validated over HTTP against the
    oracle. See docs/parity/g12-transport.md
  - [x] G12d Proxy-over-wire + `/__admin/recordings/*` (HTTP recording mode) — the outbound edge
    (`ProxyResponder`/`StubRecorder`/`RecordingSession`) extracted to `Mockifyr.Outbound` (shared by
    both facades, no facade→facade dep); a `proxyBaseUrl` stub now proxies **over the wire** (closes
    the G8 wire gap, previously validated only in-process) and record-through-proxy (`start`/`stop`/
    `status`/`snapshot`) captures generated stubs that replay on the real oracle. Validated over HTTP.
    See docs/parity/g12-transport.md
  - [ ] G12e `/__admin/ext/*` (admin-extension routing) + standalone/deploy & config (host config,
    `--port`/`--https-port`, mappings-dir load) — final G12 slice
- [ ] **G13** gRPC extension
- [ ] **G14** GraphQL extension
- [ ] **G15** Message-based/WebSocket + JWT + Faker + multi-domain
- [ ] **G16** Persistence providers (FileBased/LiteDB/Postgres/Redis) + change-feed reload

## Post-phase (not now — architecture is ready for it)

- [ ] UI / dashboard (dark mode, design system, omercelik.dev brand language)
