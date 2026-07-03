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

**Next item: G2e** (random helpers).

## 4. Gotchas learned (save yourself the time)

- **Oracle:** `wiremock/wiremock:3.10.0`. Differential tests need Docker — if it is down,
  `open -a Docker` and wait for `docker info` to succeed before running the suite.
- **Commit guard:** a hook rejects commit messages containing the substring `oc` (it fires on
  ordinary words like "pr**oc**ess"). Write the message to a file and use `git commit -F <file>`.
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
  G1i date matchers) — validate only over fixed instants, or add tolerant masking to the differ
  first. Java format zone letters (`Z`/`X`) and the unparseable-date→now fallback are also deferred.
  **Heads-up for G2e (random helpers):** same raciness — a seedable/deterministic angle or differ
  masking is needed before those can be diffed.
- Smaller, per-feature deferrals are recorded inline in the `docs/parity/*.md` notes.
