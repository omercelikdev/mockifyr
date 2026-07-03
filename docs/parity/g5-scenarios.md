# Parity notes — G5 Stateful scenarios

Verified WireMock scenario behaviors against the oracle (`wiremock/wiremock:3.10.0`). See
[README](README.md) for the format.

## Scenario state machine (G5)

- **Group / item:** G5 — validated against the oracle.
- **Shape.** A stub is bound to a scenario by `scenarioName`, is **eligible** only when the scenario
  is in `requiredScenarioState` (when present), and **transitions** the scenario to
  `newScenarioState` (when present) after it serves. Parsed into `ScenarioBinding`; the engine
  already reads eligibility (`IsEligible`) and writes the transition (`ApplyTransition`).
- **Default start state is `Started`** — a scenario with no recorded state behaves as `Started`
  (matching WireMock's `Scenario.STARTED`), so a stub with `requiredScenarioState: "Started"` is
  eligible from the outset. Verified end-to-end.
- **Walk verified.** Three stubs on one URL (`Started → step2 → step3`, terminal) driven four times
  returned `first`, `second`, `third`, `third` — the terminal state stays put — identically on both
  sides.
- **Per-scenario isolation.** Two independent scenarios advance separately: driving scenario `A` does
  not change scenario `B`'s state (state is keyed by `(tenant, scenarioName)`).
- **A stub with `scenarioName` but no `requiredScenarioState`** is eligible in any state (the engine
  treats a null required state as "always eligible").
- **Reset.** `POST /__admin/scenarios/reset` returns every scenario to `Started` (used implicitly —
  the harness's per-case `reset` clears scenario state between cases).
- **Harness note.** Scenarios need multiple stubs and ordered requests: the case loads a
  `{"mappings":[…]}` bundle into both sides, then drives the request sequence, diffing each step. Both
  sides keep their own scenario state across the sequence (the oracle in its journal, Mockifyr in its
  `IScenarioStateStore`).
- **Deferred:** `POST /__admin/scenarios/{name}/state` (set state directly) and the scenarios admin
  listing — admin-surface features that arrive with G7.
- **Regression case:** `G5ScenarioTests.Scenario_StateMachine`.
