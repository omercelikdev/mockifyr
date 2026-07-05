# G14 — GraphQL extension

GraphQL parity by matcher, not transport: a GraphQL request is an ordinary HTTP `POST /graphql` with a
`{"query": …}` body, so the whole serving path is unchanged — GraphQL adds a **request matcher** that
understands GraphQL query equivalence. Validated against the community WireMock GraphQL extension
(`graphql-body-matcher`) as the oracle.

## Query matching (G14a)

- **Group / item:** G14a — validated over HTTP against WireMock + the GraphQL extension.
- **A GraphQL stub is an ordinary stub** with a `customMatcher` named `graphql-body-matcher` and a
  `parameters.query` holding the expected query (discovered from the extension's source; the earlier
  guesses `expectedQuery`/`requestJson` are *not* the keys). The adapter recognizes that name and
  builds a `GraphqlQueryMatcher(query)`; any other `customMatcher` name still resolves via the G10
  registry.
- **Normalization (matches the reference exactly).** The extension parses both the request and expected
  queries, runs graphql-java's `AstSorter`, then compares with `AstComparator` — so equal queries match
  **regardless of whitespace and field/argument order**. `GraphqlQueryMatcher` reproduces this with
  GraphQL-Parser: parse → recursively sort selections and arguments bottom-up (by their printed form)
  → print canonically → string-compare. A syntax error (either side) is a no-match.
- **Learned behavior — field/argument order is normalized, not just whitespace.** Confirmed on the
  oracle: `{ hero { id name } }` matches an expected `{ hero { name id } }`, and reordered arguments
  match too. So whitespace-only normalization would diverge; the AST sort is required.
- **Validation.** The same stub is loaded into the oracle and Mockifyr; five query variants — exact,
  reformatted (whitespace), reordered fields, a different query, and a syntactically invalid one — are
  POSTed to `/graphql` against each. The match/no-match decision agrees on every variant.
- **Deferred to G14b (explicitly tracked — not a silent gap):** `variables` and `operationName`
  matching. Also deferred: fragments/directives ordering beyond the common cases; GraphQL response
  templating.
- **Regression case:** `G14aGraphqlTests.QueryMatching_AgreesWithTheOracle`.

## Variables + operation name (G14b)

- **Group / item:** G14b — validated over HTTP against WireMock + the GraphQL extension.
- **All three must agree.** The extension aggregates (AND) three sub-matches; `GraphqlQueryMatcher` now
  does the same: the query (G14a), the **variables**, and the **operationName**.
- **Variables** (`parameters.variables`, a JSON object). When the stub specifies them, the request's
  `variables` must be **semantically JSON-equal** (reusing the `equalToJson` comparator with no
  array-order / extra-element leniency — the extension uses `EqualToJsonPattern(false, false)`). When
  the stub does **not** specify variables, the request must have **none** (the extension uses
  `AbsentPattern`). A `"variables": null` in the body reads the same as absent, matching the extension.
- **Operation name** (`parameters.operationName`, a string). When specified, exact string equality;
  when not, the request must have none.
- **Validation.** A stub constraining query + `variables {"id":"1"}` + `operationName "Hero"`, against
  five request variants — all-match (query reformatted), wrong variable value, missing variables, wrong
  operation name, missing operation name. The match/no-match decision agrees with the oracle on each.
- **Deferred (tracked):** GraphQL response templating (echoing request variables into the response).
- **Regression case:** `G14bGraphqlVariablesTests.VariablesAndOperationName_AgreeWithTheOracle`.
