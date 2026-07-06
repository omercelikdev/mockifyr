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
    **extraction** (`{{request.path.<name>}}`) landed as a later backfill — see G2b
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
    (dual string/object model, via a custom Handlebars object descriptor) landed as a backfill and are
    oracle-validated (`Templating_PathVariables`); built-in helpers are G2c–G2h. See
    docs/parity/g2-response.md
  - [x] G2c data helpers — `jsonPath` (scalar, empty-on-miss, compact array, Jackson-pretty object),
    `xPath` (text/attr/string/count values + XML element serialization), `regexExtract`
    (whole-match, capture-group variable, `default=` / error string), `formData` (first value +
    `urlDecode`), `parseJson` (navigable variable — inline **and** the `{{#parseJson}}…{{/parseJson}}`
    block form, block body rendered-then-parsed), validated against the oracle. Multi-value `formData`
    indexing is deferred. See docs/parity/g2-response.md
  - [x] G2d date helpers — `parseDate` (ISO-8601 + Java `SimpleDateFormat` input) composed into
    `date` (Java format patterns incl. `E`/`a`/`S` translation, `epoch`/`unix`, default ISO, plural
    `offset=` units), validated against the oracle over fixed instants. The **`now`** helper (default
    ISO + `offset=` + `format=`) landed as a backfill, **structurally** validated (racy output can't be
    byte-diffed — both sides must produce a correctly-formatted value inside the request's time window;
    `Templating_NowHelper`). `now` `timezone=`/`truncate=` and the unparseable-date fallback remain
    deferred; `timezone=` is ignored on a parsed instant to match the oracle. See docs/parity/g2-response.md
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
- [x] **G11** HTTPS/TLS + HTTP/2
  - [x] G11a HTTPS/TLS serving — `MockifyrHost` binds `--https-port` on Kestrel with an ephemeral
    self-signed cert (`SelfSignedCertificate`), like WireMock's default. Validated over a **real TLS
    connection** against the oracle's own `--https-port` listener (status/body/headers diff). The
    `--https-port` hook deferred from G12f. See docs/parity/g11-tls-http2.md
  - [x] G11b HTTP/2 — both Kestrel listeners `Http1AndHttp2`; **h2 over TLS (ALPN)** validated against
    the oracle (both negotiate `response.Version` == 2.0, matching body). Plaintext prior-knowledge
    h2c is *not* asserted — the oracle answers it nondeterministically (h2 vs `HTTP_1_1_REQUIRED`); the
    plaintext listener is left h2c-capable to match. Configured keystore / mTLS deferred. See
    docs/parity/g11-tls-http2.md
- [x] **G12** Transport HTTP facade + standalone/deploy + config
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
  - [x] G12e `/__admin/ext/*` admin-extension routing — `IAdminApiExtension` made dispatchable
    (transport-agnostic `AdminApiRequest`/`AdminApiResponse` + `HandleAsync`); the admin facade routes
    any request under `/__admin/ext/<prefix>/*` to the extension whose `RoutePrefix` is that first
    segment (subpath + query + body lowered, extension owns everything below, unknown prefix → 404).
    Registered via `AddMockifyr(cfg => cfg.AddAdminApiExtension(…))`. Like the other extension seams
    (G10) there is no WireMock oracle, so validated in-process over HTTP. See docs/parity/g12-transport.md
  - [x] G12f Standalone/deploy & config — `MockifyrHost.Build(args)` binds the mock-serving port
    (`--port`, WireMock default `8080`) and, given a `--root-dir`, loads its `mappings/*.json` (single
    stubs + `{"mappings":[…]}` bundles, filename order) into the default tenant at startup via the
    `IMappingsLoader` seam (`DirectoryMappingsLoader`). `Program` is now thin. Deploy/config plumbing
    (the loaded stubs' serving is already oracle-covered), so validated in-process over HTTP: the
    loader parses a temp dir, and a real Kestrel host on an ephemeral port serves a disk-loaded stub.
    `--https-port` landed in **G11a** (needs TLS). See docs/parity/g12-transport.md
- [ ] **G13** gRPC extension
  - [x] G13a Unary serving — `Mockifyr.Facade.Grpc`: a descriptor-driven `ProtobufJsonCodec`
    (protobuf ↔ proto3-JSON via `CodedInputStream`/`CodedOutputStream`, since C# has no runtime
    `DynamicMessage`) + a gRPC HTTP/2 middleware that decodes the call, routes it through the
    **unchanged** `StubEngine` as a POST to `/service/method`, and re-encodes the response. Descriptors
    from `<root-dir>/grpc/*.dsc`. Validated against the **official WireMock gRPC extension** oracle over
    TLS (a unary `SayHello` reply matches). See docs/parity/g13-grpc.md
  - [x] G13b Codec expansion — `ProtobufJsonCodec` now covers `enum` (by value name), `map` (as a JSON
    object ↔ entry messages), and repeated fields (packed + unpacked), driven by the descriptor.
    Validated against the oracle with a `Describe` call carrying repeated/enum/map in and a packed
    repeated `int32` out. `oneof`/wrappers, streaming, status responses, and gRPC admin reset deferred.
    See docs/parity/g13-grpc.md
  - [x] G13c oneof + well-known wrappers — `oneof` is transparent (a member is an ordinary tagged field,
    so only the set one is read/written — no codec change). The wrapper types (`StringValue`/`Int32Value`/
    `Int64Value`/`BoolValue`/…) render as their **bare inner scalar** (not `{"value":…}`); the codec
    detects them by full name and unwraps on decode (synthesizing the type default when the wire omits it)
    / re-wraps on encode, confined to the message path so they work anywhere a message can. Validated
    over the wire with a `Wrapped` call carrying wrappers + a oneof in both request and reply; the `.dsc`
    was regenerated with `--include_imports`. Streaming/status/admin-reset deferred. See docs/parity/g13-grpc.md
- [ ] **G14** GraphQL extension
  - [x] G14a Query matching — a `GraphqlQueryMatcher` (parse + AST-sort + canonical print, so equal
    queries match regardless of whitespace and field/argument order) via GraphQL-Parser; the adapter
    recognizes the `graphql-body-matcher` `customMatcher` (`parameters.query`). Validated against the
    **community WireMock GraphQL extension** oracle across five query variants (exact/reformatted/
    reordered/different/invalid all agree). See docs/parity/g14-graphql.md
  - [x] G14b Variables + operationName — `GraphqlQueryMatcher` now aggregates query + `variables`
    (semantic JSON-equal, or absent when unspecified) + `operationName` (string-equal, or absent), the
    way the extension does. Validated against the oracle across five request variants. GraphQL response
    templating deferred. See docs/parity/g14-graphql.md
- [ ] **G15** Message-based/WebSocket + JWT + Faker + multi-domain
  - [x] G15a Faker / `random` helper — `{{random 'Class.method'}}` renders fake data (Datafaker-style
    expression) via **Bogus** (Datafaker's .NET counterpart), a curated provider subset; unknown
    expression → WireMock's error string. Racy output, so **structurally** validated against the
    WireMock faker-extension oracle (each field satisfies a format contract on both sides over many
    iterations). See docs/parity/g15-extras.md
  - [x] G15b JWT / `jwt` helper — `{{jwt sub=… role=…}}` renders an HS256-signed JWT with claim defaults
    matching WireMock (`iss`/`aud`/`sub`/`iat`/`exp`, default maxAge 36500 days) + custom claims;
    hand-rolled HMAC (no new dep). Random secret + racy `iat`, so validated by **content parity**
    (decoded header + non-time claims match the JWT-extension oracle; `iat`/`exp`/signature structural).
    RS256/JWKS, configurable secret, `nbf`, array claims deferred. See docs/parity/g15-extras.md
  - [x] G15c Multi-domain — `request.host` / `request.port` / `request.scheme` matching so one instance
    serves many domains. `scheme` is a plain string, `host` a full StringValuePattern (equalTo/matches/…),
    `port` an integer. Byte-diffed against the oracle; the run **confirmed** WireMock derives host+port
    from the `Host` header and scheme from the listener. `Host`-header-less port fallback + IPv6 literals
    deferred. See docs/parity/g15-extras.md
- [x] **G16** Persistence providers (FileBased/LiteDB/Postgres/Redis) + change-feed reload
  - [x] G16a File-based persistence — an `IStubPersistence` seam (no-op default) the management-path
    handlers call; `--root-dir` registers `FileSystemStubPersistence`, writing each stub as an
    id-stamped WireMock JSON file to the **same** `<root>/mappings` the G12f loader reloads, so
    create/import/delete/reset survive a restart with stable ids. Durability validated over the admin
    API; the reloaded stub's served response is diffed against the oracle. Multi-tenant reload,
    Postgres (G16c)/Redis (G16d), and change-feed (G16e) deferred. See docs/parity/g16-persistence.md
  - [x] G16b LiteDB persistence — `LiteDbStubPersistence` + `LiteDbMappingsLoader` behind the same
    `IStubPersistence`/`IMappingsLoader` seams (proving multi-provider), each stub a document in an
    embedded single-file db; `--litedb <path>` turns it on (DI-owned `LiteDatabase` singleton).
    Durability validated over the admin API; reloaded response diffed against the oracle. Redis
    (G16d)/change-feed (G16e) deferred. See docs/parity/g16-persistence.md
  - [x] G16c PostgreSQL persistence — `PostgresStubPersistence` + `PostgresMappingsLoader` (Npgsql)
    behind the same seams; each stub a row, upserted, with a shared `CREATE TABLE IF NOT EXISTS`.
    `--postgres <connstr>` turns it on. Durability validated against a **real Postgres container**
    (Testcontainers); reloaded response diffed against the oracle. Redis (G16d)/change-feed (G16e)
    deferred. See docs/parity/g16-persistence.md
  - [x] G16d Redis persistence — `RedisStubPersistence` + `RedisMappingsLoader` (StackExchange.Redis)
    behind the same seams; each tenant's stubs a Redis hash keyed by id. `--redis <connstr>` turns it
    on. Durability validated against a **real Redis container** (Testcontainers); reloaded response
    diffed against the oracle. See docs/parity/g16-persistence.md
  - [x] G16e Change-feed reload — every `RedisStubPersistence` mutation announces on a pub/sub channel;
    `--change-feed` opts a host into a `RedisChangeFeedReloader` (`IHostedService`) that reloads +
    reconciles its store on any announcement. Multi-instance coherence validated with **two live hosts**
    sharing Redis (create/delete on one propagates to the other without a restart). Postgres
    LISTEN/NOTIFY + multi-tenant reload deferred. See docs/parity/g16-persistence.md

## Post-phase (not now — architecture is ready for it)

- [ ] UI / dashboard (dark mode, design system, omercelik.dev brand language)
