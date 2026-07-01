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
  - [ ] G1b URL advanced (urlMatching, urlPathMatching, urlPathTemplate + named path vars)
  - [ ] G1c header/query/cookie matchers (+ multi-value) — header/query `equalTo`/`contains`/
    `absent` fuzz-validated across the corpus; cookie, `doesNotMatch`, `equalToIgnoreCase`,
    multi-value still pending
  - [ ] G1d body basic (equalTo, binaryEqualTo, contains, matches) — `equalTo`/`contains`/
    `matches` fuzz-validated across the corpus; `binaryEqualTo` pending
  - [x] **Fuzzing generator** (brief §5) — deterministic seed-driven `MatcherScenarios` emit
    hundreds of corpus-spanning probes; the property suite asserts the match decision agrees
    with the oracle. It already caught the empty-body divergence above.
  - [ ] G1e equalToJson (ignoreArrayOrder × ignoreExtraElements)
  - [ ] G1f matchesJsonPath
  - [ ] G1g equalToXml / matchesXPath
  - [ ] G1h matchesJsonSchema
  - [ ] G1i date/time matchers
  - [ ] G1j number matchers
  - [ ] G1k logic + basicAuth + form/multipart + clientIp + stub priority/selection
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
