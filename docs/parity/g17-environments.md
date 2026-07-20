# G17 — Environments (tenant-scoped `{{key}}` config)

Environments have **no WireMock counterpart**, so there is no oracle to diff against. Per the standing
rule for such features, the validation method is stated up front: pure-logic unit coverage for the
substitution contract, behavioral self-tests for the two claims the issues make, and an end-to-end
script driving only the public HTTP surface. See `docs/decisions/0008-serve-time-environment-resolution.md`
for why the feature is server-side at all.

## What it is

A tenant owns **keys**; each key holds several named **values**, one of which is **active**. A stub
referencing `{{key}}` is stored with that expression verbatim and resolved when the stub is served —
so switching the active value changes every stub using the key, with no re-save.

## Where it runs (load-bearing)

- **Before Handlebars, and before the `response-template` transformer guard.** After the guard, the
  pass would silently skip every stub that did not opt into templating — which is most stubs, and the
  bug would look like "environments just don't work for my stub."
- **Response body + headers, proxy target, webhook URL/body/headers.** The proxy target needed
  explicit work: `ProxyDirective.BaseUrl` was previously *never* templated (both renderer branches
  copied it verbatim), so a `{{key}}` proxy target would have reached the outbound client as literal
  text. Webhooks needed `IServeEventTemplateRenderer` widened to carry a `TenantId`.

## Learned: a shared `{{…}}` namespace is safe only if the pass is selective

The original UI-only design (#157) resolved in the browser specifically to avoid this collision. The
substitution is safe here because it replaces **only bare identifiers that resolve to a key the tenant
has defined**. Everything else — `{{now}}`, `{{request.path}}`, `{{random 'X.y'}}`, `{{#each}}`,
`{{typo}}` — passes through byte-identical. Consequences worth knowing:

- An **undefined** reference is never blanked by this pass. On a non-templated stub it survives as
  literal `{{typo}}` in the response, which is diagnosable; the dashboard also warns while editing.
- A **substituted value is not rescanned**, so a value that itself looks like `{{other}}` does not
  chain-resolve. One pass, no recursion, no cycles.
- Lookup is **case-sensitive**: `{{BaseUrl}}` does not resolve `baseUrl`, it falls through to
  Handlebars. This keeps the pass predictable rather than helpfully wrong.

## Learned: the only real collision is a helper-named key, so it is refused at write time

A key named `now` would shadow `{{now}}` in every stub of that tenant, and nothing in the stub would
explain why the timestamp stopped appearing. Rather than manage that at read time, the admin API
**rejects** the create (`Environment.ReservedKey`, HTTP 400) against a list mirroring the built-in
helper names. The dashboard repeats the list to turn the 400 into inline feedback, but the server
remains authoritative. This is what keeps the bare-identifier surface unambiguous.

## Tenant scoping (issue #166)

Enforced in the store, not in the dashboard: every `IEnvironmentStore` / `IEnvironmentResolver` method
takes a `TenantId` and there is no tenant-less overload. Consequences that are tested explicitly:

- A key defined in tenant A is absent from tenant B's list, and B's stub referencing the same name
  resolves nothing — it does **not** inherit A's value.
- `DELETE` and the active-value `PUT` return **404** for a key the calling tenant does not own, rather
  than succeeding silently or reaching across.
- The same key name holds independent values per tenant. The Postgres schema states this directly:
  `PRIMARY KEY (tenant, key)`.
- `RenderContext.Tenant` is `required` — a future call site that forgets the scope is a **compile
  error**, not a runtime leak.

## Migration from the #157 shape

Old `localStorage` environments (`{name, baseUrl}`) convert to one key per environment with a single
value named `default`, preserving what `{{name}}` meant. They migrate into **the tenant the operator
is currently in** — the legacy data carried no tenant, so writing it to every tenant would recreate
the leak #166 reports. The legacy blob is removed only after the server accepts the writes, so a
failed migration retries rather than losing data.

## Deferred (tracked)

- Change-feed reload (G16e/f) does not yet cover environments: `IEnvironmentStore.GetTenants()` exists
  for it, but no reconciler subscribes. Multi-instance hosts see key changes after a restart.
- No import/export of environments alongside the mappings bundle.
- Values are stored in plaintext; a secret-typed value (masked in the dashboard) is not modelled.

## Regression cases

- `EnvironmentSubstitutionTests` — the selectivity contract, the reserved/well-formed pairing, and
  `EnvironmentKey.Resolve()` including the deleted-active-value case.
- `G17EnvironmentTests` — dynamic resolution (active-value switch reaching a saved stub), per-key
  independence, verbatim storage, non-templated stubs, and the full tenant-isolation matrix.
