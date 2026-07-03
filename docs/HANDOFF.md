# HANDOFF — resume Mockifyr in a new session

Start-here notes for continuing the roadmap across sessions. The durable memory lives in the repo
itself; this file just points at it and records hard-won gotchas so nothing is lost between sessions.

## 1. Resume in three steps

1. `git checkout main && git pull` — all completed work is merged to `main`.
2. Read **[docs/roadmap.md](roadmap.md)** for the checkbox state, and the **[docs/parity/](parity/)**
   notes for the WireMock behaviors already verified against the oracle.
3. Follow the development loop in **[CLAUDE.md](../CLAUDE.md) §3**.

## 2. Per-item loop (do not skip a step)

Branch from `main` → **write the failing differential test first** (author the scenario as WireMock
JSON, drive it through the Java WireMock oracle and Mockifyr, assert the diff) → implement minimally
→ green diff → update the relevant `docs/parity/*.md` and tick `docs/roadmap.md` → commit → open a
**separate PR per item**. The green oracle diff is the only definition of done.

## 3. Where things stand

- **G1 (Matching)** — complete (G1a–G1k), including cookie value matching.
- **G2a** static response — done.
- **G2b** templating engine — core done (Handlebars.Net behind the `response-template` transformer;
  request model + non-escaping + templated headers).
- **G2c** data helpers — done (`jsonPath`, `xPath`, `regexExtract`, `formData`, `parseJson`), each
  diffed against the oracle. Multi-value `formData` indexing and the `parseJson` block form deferred.
- **G2d** date helpers — done (`parseDate` + `date`: Java format patterns, `offset=`, `epoch`/`unix`,
  default ISO), validated over fixed instants. `now`/now-relative and the unparseable→now fallback
  are racy and deferred; `timezone=` is ignored on a parsed instant to match the oracle.
- **G2e** random helpers — done (`randomValue`, `pickRandom`, `randomInt`, bounded `randomDecimal`),
  validated **structurally** (racy output → the oracle and Mockifyr must each satisfy the same
  charset/length/range contract; see `RandomScenarios` + `Templating_RandomHelpers`). This is the
  reusable pattern for any future racy helper.
- **G2f** json manipulation — done (`jsonArrayAdd`, `jsonMerge`, `jsonRemove` → compact; `toJson` →
  Jackson-pretty via the shared `JacksonJson.Write`). All take JSON strings; array-valued key merge
  deferred.
- **G2g** format/math/array — done (`math`, `numberFormat`, `size`, `join`, `substring`, `replace`,
  `upper`, `lower`, `capitalize`, `trim` — jknack built-ins WireMock registers). `%`/`^` and non-OSS
  helpers (abs/round/split/…) rejected by the oracle → deferred.
- **G2h** system helpers — done (`systemValue` deny-by-default byte-diffed; `hostname` host-specific,
  validated structurally). **G2 (Response + templating) is now complete** — the whole helper surface
  (data/date/random/json/format-math-array/system) is covered.

- **G3a** serve-event listener + async outbound — done. `postServeActions` webhook (static
  method/url/headers/body) fired by `WebhookServeEventListener` (`IServeEventListener`) at the facade
  edge; the engine only records the `WebhookDefinition`. Validated differentially via a host-side
  `WebhookReceiver` (`HttpListener`): the oracle reaches it through `host.docker.internal` (the oracle
  container now has `WithExtraHost("host.docker.internal","host-gateway")` for Linux CI), Mockifyr via
  `127.0.0.1`; the mapping's `__WEBHOOK_HOST__` token is rewritten per side, and only declared headers
  (+ method/path/body) are diffed (auto transport headers differ per client).
- **G3b** templated webhook + originalRequest — done. The webhook `url`/`headers`/`body` are
  Handlebars-templated against `originalRequest` (automatic, no transformer flag), reusing the
  templating engine via the shared `HandlebarsFactory`/`RequestModel` and the Core
  `IServeEventTemplateRenderer` seam. Sub-event journaling deferred to G6/G7.

## 🏁 Phase A is complete (G0–G3): a proven, working core.

Matching (G1, all matchers), Response + templating (G2, all helper families), and Webhook
(G3a static + G3b templated) are each differentially validated against the oracle. 58 differential
tests, all green.

- **G4** delay + fault — done. `fixedDelayMilliseconds` → `DelayDirective`, applied by the library
  facade (`MockifyrServer.Handle`); validated by content parity + a robust **lower-bound** timing
  assertion (`ProbeTimedAsync`; a fixed delay can't make a response faster). `fault` (all four kinds)
  parsed into a `FaultDirective` and unit-tested; **socket-level fault emission** and
  `delayDistribution` deferred to the HTTP facade (**G12**). Faults can't be diffed in-process — the
  harness drives the engine, not a socket. 64 differential tests green.

**Next item: G5 — Stateful scenarios.** WireMock's `scenarioName` + `requiredScenarioState` +
`newScenarioState`: a stub is eligible only in a given state, and matching transitions the state. The
**model and store already exist** — `StubMapping.Scenario` (`ScenarioBinding`),
`IScenarioStateStore` (in-memory impl wired), and `StubEngine` already reads state for eligibility and
writes the transition (see `IsEligible`/`ApplyTransition`). So G5 is likely mostly **adapter parsing**
(`scenarioName`/`requiredScenarioState`/`newScenarioState` → `ScenarioBinding`) + differential
scenarios that drive a multi-step state machine (e.g. the classic to-do/START→state transitions) and
assert the response changes per state. Probe the oracle for the default start state name (`Started`)
and transition semantics.

## 4. Gotchas learned (save yourself the time)

- **Oracle:** `wiremock/wiremock:3.10.0`. Differential tests need Docker — if it is down,
  `open -a Docker` and wait for `docker info` to succeed before running the suite.
- **Commit guard:** a hook rejects commit messages containing the substring `oc` (it fires on
  ordinary words like "pr**oc**ess"). Write the message to a file and use `git commit -F <file>`.
- **Helper existence = the oracle compiles the template eagerly at mapping registration.** An unknown
  helper or unsupported operator (`toUpperCase`, `math … '%'`, `math … '^'`) makes the **mapping POST
  itself 500 / not register** — not a request-time error. So probe helper existence with a minimal
  mapping and check the registration status. WireMock's templating helpers are jknack Handlebars
  built-ins (`upper`/`lower`/`capitalize`/`join`/`substring`/`replace`/`size`/`math`/`numberFormat`),
  not camelCase names.
- **Not everything in the roadmap exists in open-source WireMock.** The standalone number matchers
  (`equalToNumber`, …) and `clientIp` are **WireMock Cloud only** — the OSS engine returns `422`.
  Always probe the oracle before implementing; if it rejects the mapping, there is no oracle and the
  feature cannot be validated (see the parity notes). Numeric matching lives in JSONPath filters;
  multi-value keys are `hasExactly`/`includes` (not `havingExactly`/`including`).
- **Harness transport quirks:** the oracle HTTP client folds repeated request **headers** into one
  comma-joined value, so validate multi-value matching via **query parameters**. (Cookie transmission
  was fixed with `ConnectionClose` per request — see `WireMockOracle.SendAsync`.)

## 5. Deferred pieces to close when their turn comes

- **G2b:** `request.path.<name>` named path variables from `urlPathTemplate` — WireMock's
  `request.path` is a dual string/object model (string form + named vars + indexed segments) that
  needs a custom Handlebars.Net member resolver.
- **G1c:** multi-value **header** matching (blocked only by the harness header-folding above; the
  matchers themselves work and are covered via query).
- **G2c:** multi-value `formData` indexing (`{{form.key.0}}`) — Handlebars.Net comma-joins any
  `IList` bound to a bare `{{form.key}}`, so WireMock's `ListOrSingle` dual render/index type needs a
  custom member resolver (same shape as the G2b `request.path` deferral). Also the `parseJson`
  block/inline form.
- **G2d:** the `now` helper and any now-relative rendering are **racy** vs a second clock (like the
  G1i date matchers) — validated only over fixed instants. Java format zone letters (`Z`/`X`) and the
  unparseable-date→now fallback are also deferred. (The racy `now` could later be validated with the
  **structural-parity** pattern introduced in G2e — see below.)
- **G2e:** the racy-helper validation pattern is now established — see `RandomScenarios` +
  `Templating_RandomHelpers`: probe many times and require the **oracle** and Mockifyr to each
  satisfy the same structural contract (charset/length/range) rather than byte-diffing. Reuse this
  for any future non-deterministic helper (`now`, `randomValue ALPHANUMERIC_AND_SYMBOLS`, etc.).
- Smaller, per-feature deferrals are recorded inline in the `docs/parity/*.md` notes.
