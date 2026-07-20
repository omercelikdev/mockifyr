# 0008 — Environments resolve at serve time, in a pre-Handlebars pass, scoped by tenant

## Status

Accepted. Implements issues #165 and #166; supersedes the UI-only design of #157.

## Context

#157 added Postman-style environments as a **browser-only** feature: `{name, baseUrl}` pairs in
`localStorage`, with `{{name}}` substituted by the dashboard at the moment a URL was used, so the
saved mapping carried a plain resolved URL. That was a deliberate choice at the time — a `{{name}}`
surviving into a mapping shares a namespace with response templating, and the server would have
rendered it as empty.

Two reports showed the design could not hold:

- **#165** — a key needs *several* values with one active, selectable per key; and the reference must
  stay dynamic. Freezing the value at save time means changing environments requires re-saving every
  stub that mentions one.
- **#166** — `localStorage` has no tenant, so every tenant saw (and could use) every other tenant's
  values. For a product whose first-class invariant is tenant isolation (ADR 0003), that is a leak.

"Dynamic" and "tenant-scoped" together force the feature server-side: only the engine knows, at the
moment of serving, which tenant a request belongs to.

Three obstacles existed in the code:

1. `RenderContext` carried no tenant, so the renderer could not resolve tenant-scoped anything.
2. `ProxyDirective.BaseUrl` was **never** templated — both branches of the renderer copied it verbatim.
3. `IServeEventTemplateRenderer` (webhooks) took only a `CanonicalRequest`, no tenant.

## Decision

**Environments are a tenant-scoped server-side entity, resolved in a substitution pass that runs
before Handlebars, and only for names the tenant has actually defined.**

- `EnvironmentKey(Key, ActiveValue, Values[])` in Core, behind `IEnvironmentStore` (admin) and
  `IEnvironmentResolver` (serve path). Every method takes a `TenantId`; there is no tenant-less
  overload, per ADR 0003.
- `RenderContext.Tenant` is `required`. Making it required rather than defaulted means a future call
  site that forgets the scope fails to compile instead of silently serving the default tenant's values.
- The pass runs in `TemplatingResponseRenderer.Render` **before** the `response-template` transformer
  guard. Placing it after the guard would silently skip every stub that did not opt into templating —
  which is most of them.
- It substitutes **only bare identifiers that resolve to a defined key**. `{{now}}`,
  `{{request.path}}`, `{{random 'X.y'}}` and `{{#each}}` are left byte-identical for Handlebars.
- Proxy targets and webhook URL/body/headers are substituted too, since those are the fields the
  feature exists for.

### Why the bare `{{key}}` surface is safe

It shares the `{{…}}` delimiter with Handlebars, which is why #157 avoided it. Two properties make it
safe here:

- **Selectivity.** An undefined name is never touched, so a reference can only ever *add* meaning to
  text Handlebars would have rendered empty. Nothing that works today changes behavior.
- **A closed collision set.** The only real hazard is a key named after a built-in helper — a key
  called `now` would shadow `{{now}}` in every stub of that tenant, with nothing in the stub to
  explain it. So the admin API **refuses** to create one (`Environment.ReservedKey`, 400). The
  ambiguity is eliminated at the write end rather than managed at the read end.

The alternative, namespacing as `{{env.key}}`, is collision-proof by construction but diverges from
the syntax the issue specifies and would need a migration. Given the closed collision set, the cost
was not justified.

## Consequences

- Environments survive restarts: `IEnvironmentPersistence` + `IEnvironmentsLoader` have file, LiteDB,
  Postgres and Redis providers, mirroring G16. The Postgres primary key is `(tenant, key)` — the
  database-level expression of #166.
- Startup restores **every** tenant's keys, not just the default (as mappings do). A key that failed
  to return would not fail loudly; the stub referencing it would quietly serve `{{key}}`.
- The dashboard no longer resolves anything into a stub it is about to save. Its resolution code is
  preview-only, mirroring the server pass so the operator sees what will be served.
- Existing `localStorage` environments migrate on first visit, into **the tenant the operator is
  currently in** — the legacy data carried no tenant, so fanning it out to all of them would recreate
  the very leak #166 reports.
- `IServeEventTemplateRenderer.Render` gained a `TenantId` parameter (a breaking change to a public
  Core contract, taken deliberately: a webhook URL is exactly the kind of field that references a key).
- The engine remains pure: the resolver is a Core contract, substitution is a pure string pass, and
  all I/O stays at the host edge.

## Verification

There is **no WireMock oracle** for this — environments are not a WireMock concept. Per the standing
rule that such features state their method, this is validated by:

- `EnvironmentSubstitutionTests` — the selectivity contract, exhaustively: every Handlebars construct
  must survive a substitution pass untouched.
- `G17EnvironmentTests` — the two claims the issues make: an active-value switch reaching an
  already-saved stub with no re-save, and tenant isolation for read, resolve, delete, switch and reset.
- An end-to-end script against a live host driving only the public HTTP surface (26 checks), including
  a restart to prove keys and their active selection reload.
