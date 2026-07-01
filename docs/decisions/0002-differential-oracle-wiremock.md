# 0002 — Differential validation, oracle = Java WireMock

**Status:** Accepted · **Date:** 2026-07-01

## Context
Mockifyr's value is in faithfully reproducing WireMock's behavior. That behavior depends on
Java libraries (Handlebars.java, Jayway JsonPath, Saxon/JAXP XPath, XMLUnit). "Looks like it
works" happy-path code — or an AI approving its own generated test — is not enough.

## Decision
**Differential / golden-master testing.** The oracle is a running real **Java WireMock
standalone** (in a container via Testcontainers). The same stub configuration is loaded into
both, the same request is sent to both in parallel, and the two responses are canonically
diffed. Any difference means Mockifyr is wrong. **Self-validation is forbidden** — the oracle
is always running WireMock, never the model's memory.

Inputs are not authored one by one; a **property-based / fuzzing generator** (deterministic
seed) produces them: empty/long/unicode/array-order/missing-extra fields, boundary values.
Loop: generate → send to both → diff → add the failing case to the regression suite → fix →
repeat.

## Consequences
- (+) Parity is proven at the library-semantics level, not just "is the matcher correct".
- (+) The regression suite grows with every failing case.
- (−) The harness depends on the JVM/containers (CI needs Docker).
- **Known limit:** the harness only catches what it can compare (content/status/headers).
  Timing, socket-level faults, TLS handshake, and concurrency do not show up in a byte diff
  and are handled separately.
