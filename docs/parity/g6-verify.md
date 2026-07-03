# Parity notes — G6 Verify + near-miss diagnostics

Verified WireMock verification behaviors against the oracle (`wiremock/wiremock:3.10.0`). See
[README](README.md) for the format.

## Request verification (G6)

- **Group / item:** G6 — validated against the oracle **semantically**. The admin JSON
  (`/__admin/requests*`) carries many **volatile fields** (`clientIp`, `loggedDate`, `absoluteUrl`,
  `port`, `scheme`, `loggedDateString`…), so verification is compared by **counts and identities**,
  not byte-for-byte.
- **`count(pattern)`** = the number of **journaled requests** matching a request pattern, using the
  **same matchers as stubs**. Verified against `POST /__admin/requests/count`:
  - `{"method":"POST","url":"/api"}` → 3 (all POSTs to `/api`, regardless of body).
  - `{"method":"POST","url":"/api","bodyPatterns":[{"contains":"hello"}]}` → 2.
  - `{"method":"DELETE","url":"/api"}` → 0.
  - **`{}` (empty pattern) matches every recorded request** → 4.
- **`unmatched`** = journaled requests that matched **no stub** (`GET /__admin/requests/unmatched`).
  Verified: a request to an unstubbed URL is the sole unmatched entry; the count agrees with
  Mockifyr's `FindUnmatchedRequests`.
- **Implementation.** The engine already journals every serve (`IRequestJournal`); verification is a
  read-only query that reuses the matcher evaluation (`StubEngine.CountRequestsMatching` /
  `FindRequestsMatching` / `FindUnmatchedRequests`). No new matching logic. The query request pattern
  is parsed by the same adapter (`WireMockMappingReader.ReadRequestPattern`).
- **Harness.** `VerifyScenarios` loads stubs, replays traffic into both journals, then compares
  `count(pattern)` per pattern and the unmatched count via the oracle's admin API vs Mockifyr's
  in-process verifier (`DifferentialRunner.RunVerifyAsync`).

## Near-miss diagnostics (G6)

- The closest stubs to an unmatched request are ranked by **ascending match distance** — the same
  distance matching already computes (`MatchResult.Distance`), so near-miss needs no extra machinery
  (`StubEngine.FindNearMisses`). Validated as **pure logic**: a URL-only mismatch is strictly closer
  than a method+URL mismatch and ranks first.
- **Deferred:** **cross-engine near-miss identity** comparison (the oracle's
  `/__admin/requests/unmatched/near-misses` JSON identifies stubs differently, and matching them
  across engines is a separate effort), and the `find` request-body/identity byte comparison — the
  count comparison already exercises the matching semantics.
- **Regression cases:** `G6VerifyTests.Verify_CountAndUnmatched`,
  `G6NearMissTests.NearMisses_AreRankedByAscendingDistance`.
