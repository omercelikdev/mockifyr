# G15 — Message-based + extras (Faker, JWT, WebSocket, multi-domain)

A grab-bag of WireMock 4.x-beta and extension features. Some are ordinary templating/matching additions
(Faker) that validate cleanly against an extension oracle; others (WebSocket, still beta) have no stable
oracle and will use alternative validation. Each slice states its method.

## Faker / `random` helper (G15a)

- **Group / item:** G15a — validated **structurally** against WireMock + the faker extension.
- **`{{random 'Class.method'}}`** renders fake data from a Datafaker-style expression, mirroring
  WireMock's faker extension (the `random` helper, backed by Datafaker). Mockifyr uses **Bogus**
  (Datafaker's .NET counterpart). A curated subset of the most common providers is supported —
  `Name.firstName/lastName/fullName`, `Internet.emailAddress/url/uuid`, `Address.city/country/zipCode`,
  `Number.digit`, `Company.name`, `Lorem.word`, `PhoneNumber.phoneNumber`. An **unknown expression**
  yields WireMock's own error string (`[ERROR: Unable to evaluate the expression <expr>]`).
- **How it's validated (the racy-feature method).** Faker output is non-deterministic, so it can't be
  byte-diffed. Instead the same stub is served by both sides and, over 15 iterations, each generated
  field must satisfy a **format contract** (e.g. email regex, a 5(-4)-digit zip, a single digit, a
  UUID). The **oracle** satisfying the contract proves it is real WireMock/Datafaker behavior;
  **Mockifyr** (Bogus) satisfying the same contract is the parity claim — the same structural method
  the `randomValue` helpers (G2e) and `now` use.
- **Deferred (tracked):** the long tail of Datafaker providers beyond the curated subset (added on
  demand); locale selection.
- **Regression case:** `G15aFakerTests.FakerHelper_StructurallyMatchesTheOracle`.
