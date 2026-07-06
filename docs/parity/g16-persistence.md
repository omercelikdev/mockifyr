# G16 — Persistence providers

Durability across restarts: stub mutations made over the admin API can be persisted so a fresh
process serves them again. Persistence is infrastructure — there is no WireMock *response* semantic to
diff — but the reloaded stub's **served response is still diffed against the oracle**, so parity is
proven rather than assumed. The stores stay tenant-scoped; a persistence provider is registered behind
the same seam.

## File-based persistence (G16a)

- **Group / item:** G16a — durability validated in-process; reloaded-response parity diffed against the oracle.
- **`IStubPersistence` seam.** A Core extension seam (`Save`/`Remove`/`Clear`, tenant-scoped) that the
  management-path handlers call alongside the in-memory store. The default is `NullStubPersistence`
  (no-op — purely in-memory, nothing survives a restart). A provider registered on top wins the DI
  resolution.
- **`--root-dir` turns it on.** `MockifyrHost` registers `FileSystemStubPersistence` when `--root-dir`
  is given — persisting to the **same** `<root>/mappings` directory that `DirectoryMappingsLoader`
  (G12f) reloads on startup. This is exactly WireMock's `--root-dir` model: the persistence directory
  *is* the load directory.
- **Id stability across restart.** The reader mints a fresh id when a mapping has none
  (`ReadId` → `Guid.NewGuid()`), so the provider **stamps the stub's id** (`id` + `uuid`) into the
  saved JSON before writing `<id>.json`. A reload therefore keeps the same id — a create-then-restart
  serves the identical stub with the identical id. `ReadWithSource` returns each mapping's own source
  JSON (even from a `{"mappings":[…]}` bundle) so imports persist each element faithfully.
- **Mutations covered.** Create, import (each bundle element), delete (removes the file), and
  mappings-reset (clears the provider's `<guid>.json` files, leaving any hand-authored files alone).
- **Validation.** Over the admin API: create a stub on a host with `--root-dir`, shut it down, confirm
  the file was written, start a **fresh** host on the same dir, and serve the reloaded stub — its
  response matches the oracle's for the same mapping. Delete + reset are confirmed to stay gone after a
  restart.
- **Deferred (explicitly tracked — not a silent gap):** multi-tenant persistence *reload* (non-default
  tenants are written to per-tenant subdirectories, but startup only reloads the default tenant's
  flat dir); WireMock's `persistent:false` opt-out (all admin mutations persist when a root-dir is
  set); and the other providers — **LiteDB (G16b), Postgres (G16c), Redis (G16d)** — plus
  **change-feed reload (G16e)**, each behind this same seam.
- **Regression cases:** `G16aPersistenceTests.CreatedStub_SurvivesRestart_AndMatchesOracle`,
  `G16aPersistenceTests.DeletedStub_And_Reset_StayGoneAfterRestart`.

## LiteDB persistence (G16b)

- **Group / item:** G16b — durability validated in-process; reloaded-response parity diffed against the oracle.
- **Second provider, same seam.** `LiteDbStubPersistence` implements the same `IStubPersistence`
  contract as the file provider — proving the seam is genuinely multi-provider (retrofit-free, as the
  architecture intended). Each stub is one document `{ Id, Tenant, Json }` in an embedded single-file
  [LiteDB](https://www.litedb.org/) database; the stored JSON is id-stamped (shared `PersistableJson`
  helper) so ids round-trip identically to the file backend. `LiteDbMappingsLoader` is the
  `IMappingsLoader` counterpart that reloads the tenant's documents on startup.
- **`--litedb <path>` turns it on.** `MockifyrHost` registers the provider + loader against a shared
  `LiteDatabase` — created by DI as a singleton so the container disposes it on shutdown (flushing the
  file before the next process opens it). Storing the raw id-stamped JSON keeps persistence faithful
  without a domain → JSON serializer, exactly like the file backend.
- **Validation.** Mirrors G16a over the admin API: create on a host with `--litedb`, shut it down,
  confirm the db file exists, start a fresh host on the same file, serve the reloaded stub — its
  response matches the oracle. Delete + reset stay gone after a restart.
- **Deferred (tracked):** the remaining providers — **Postgres (G16c), Redis (G16d)** — and
  **change-feed reload (G16e)**, plus the multi-tenant-reload / `persistent:false` items noted under
  G16a.
- **Regression cases:** `G16bLiteDbPersistenceTests.CreatedStub_SurvivesRestart_AndMatchesOracle`,
  `G16bLiteDbPersistenceTests.DeletedStub_And_Reset_StayGoneAfterRestart`.

## PostgreSQL persistence (G16c)

- **Group / item:** G16c — durability validated against a real Postgres container; reloaded-response parity diffed against the oracle.
- **Third provider, a SQL backend.** `PostgresStubPersistence` implements the same `IStubPersistence`
  seam via Npgsql. Each stub is a row `(id uuid, tenant text, json text)`; the stored JSON is
  id-stamped (shared `PersistableJson`) so ids round-trip identically to the file/LiteDB backends.
  `Save` is an `INSERT … ON CONFLICT (id) DO UPDATE` upsert; connections open per operation from
  Npgsql's pool (thread-safe). `PostgresMappingsLoader` reloads the tenant's rows on startup.
- **Schema.** A shared `PostgresSchema.Ensure` runs `CREATE TABLE IF NOT EXISTS` from both the provider
  and the loader constructors, so whichever is resolved first (the loader runs at startup, before any
  mutation) finds the table in place.
- **`--postgres <connection-string>` turns it on.** Unlike the file/LiteDB backends, the durable store
  is an external database that outlives the app process — so a "restart" is just a fresh host pointed
  at the same connection string; the data was never in-process.
- **Validation.** A real `postgres:16-alpine` container (Testcontainers) alongside the WireMock oracle:
  create on a host with `--postgres`, shut it down, start a fresh host on the same database, serve the
  reloaded stub — its response matches the oracle. Delete + reset stay gone after a restart.
- **Deferred (tracked):** **Redis (G16d)** and **change-feed reload (G16e)**; connection-string
  secrets/config hardening is a deploy concern.
- **Regression cases:** `G16cPostgresPersistenceTests.CreatedStub_SurvivesRestart_AndMatchesOracle`,
  `G16cPostgresPersistenceTests.DeletedStub_And_Reset_StayGoneAfterRestart`.

## Redis persistence (G16d)

- **Group / item:** G16d — durability validated against a real Redis container; reloaded-response parity diffed against the oracle.
- **Fourth provider, a key-value backend.** `RedisStubPersistence` implements the same
  `IStubPersistence` seam via StackExchange.Redis. Each tenant's stubs live in one Redis hash
  (`mockifyr:stubs:{tenant}`) keyed by stub id, the value being the id-stamped WireMock JSON (shared
  `PersistableJson`) so ids round-trip identically to the file/LiteDB/SQL backends. `Save` → `HSET`,
  `Remove` → `HDEL`, `Clear` → `DEL` the tenant's hash. `RedisMappingsLoader` `HGETALL`s the tenant's
  hash on startup.
- **`--redis <connection-string>` turns it on.** The `IConnectionMultiplexer` (thread-safe, long-lived)
  is a DI-created singleton so the container disposes it on shutdown. Like Postgres, the store is
  external and outlives the app process — a "restart" is a fresh host on the same connection string.
- **Validation.** A real `redis:7-alpine` container (Testcontainers) alongside the WireMock oracle:
  create on a host with `--redis`, shut it down, start a fresh host on the same instance, serve the
  reloaded stub — its response matches the oracle. Delete + reset stay gone after a restart.
- **Deferred (tracked):** **change-feed reload (G16e)** — the last G16 slice: a live host reloading its
  store when another writer changes it (multi-instance coherence).
- **Regression cases:** `G16dRedisPersistenceTests.CreatedStub_SurvivesRestart_AndMatchesOracle`,
  `G16dRedisPersistenceTests.DeletedStub_And_Reset_StayGoneAfterRestart`.

## Change-feed reload (G16e)

- **Group / item:** G16e — multi-instance coherence validated with two live hosts sharing Redis. Closes the **G16** group.
- **The problem.** With a shared external store, a second instance loads the current state on startup
  (G16b–d) but does not see *later* changes another instance makes — its in-memory store drifts.
- **Redis pub/sub reload.** Every `RedisStubPersistence` mutation *announces* on a pub/sub channel
  (`mockifyr:changes`) — a publish with no subscribers is a cheap no-op, so it is always safe to emit.
  `--change-feed` opts a host into a `RedisChangeFeedReloader` (an `IHostedService`) that subscribes to
  the channel and, on any announcement, **reloads** the default tenant from the mappings loaders and
  reconciles the store: upsert what's persisted first (no empty window where a live request could miss
  a match), then prune what's gone. So a stub created (or deleted) on one instance is served (or
  stopped) by the others without a restart.
- **Validation.** Two live Mockifyr hosts share one `redis:7-alpine` container with `--change-feed`:
  a create on host A propagates to host B (B starts serving `/cf`), and a delete on A propagates too
  (B stops serving). Propagation is asynchronous (pub/sub), so the assertions poll within a timeout.
  Coherence is infrastructure — served-response parity is already oracle-covered (G16d) — so no oracle
  is needed here.
- **Regression case:** `G16eChangeFeedTests.Mutation_On_One_Instance_Propagates_To_Another`.

## Postgres change-feed reload (G16f)

- **Group / item:** G16f — the same multi-instance coherence as G16e, over PostgreSQL `LISTEN`/`NOTIFY`
  instead of Redis pub/sub.
- **`LISTEN`/`NOTIFY` reload.** Every `PostgresStubPersistence` mutation runs `NOTIFY mockifyr_changes`
  on its connection right after the write (a `NOTIFY` with no listener is a cheap no-op, so it is always
  safe to emit). `--change-feed` opts a Postgres-backed host into a `PostgresChangeFeedReloader` (an
  `IHostedService`) that holds a dedicated connection, `LISTEN mockifyr_changes`, and — driven by a
  background loop calling Npgsql's `WaitAsync` (notifications are only delivered while a wait is in
  flight) — reconciles the store on every notification via the **shared** `ChangeFeedReconciler` (upsert
  then prune, the same logic G16e uses). So a mutation on one instance is served (or stopped) by the
  others without a restart.
- **Validation.** Two live Mockifyr hosts share one `postgres:16-alpine` container with `--change-feed`:
  a create on host A propagates to host B, and a delete on A propagates too. Propagation is asynchronous,
  so the assertions poll within a timeout. Like G16e this is coherence infrastructure (served-response
  parity is oracle-covered by G16c), so no oracle is needed.
- **Regression case:** `G16fPostgresChangeFeedTests.Mutation_On_One_Instance_Propagates_To_Another`.

## Multi-tenant change-feed reload (G16g)

- **Group / item:** G16g — generalizes the change-feed reconcile from the default tenant to **every**
  tenant. Closes the last persistence edge.
- **The gap.** G16e/G16f reconciled only `TenantId.Default`, so a stub persisted for another tenant by a
  peer instance would not appear (and a non-default tenant emptied elsewhere would not be pruned).
- **All-tenant reconcile.** `IStubStore` gained `GetTenants()` (the tenants currently holding stubs) and
  a loader may implement the optional `IMultiTenantMappingsLoader.LoadAllTenants()` (the DB/KV backends —
  Postgres `SELECT tenant, json FROM stubs`; Redis a `SCAN` over `mockifyr:stubs:*`, the tenant being the
  key suffix). The shared `ChangeFeedReconciler` now gathers persisted stubs across all tenants (multi-
  tenant loaders enumerate every tenant; single-tenant loaders like a mappings directory contribute the
  default tenant only), then reconciles the **union** of the reloaded tenants and the store's current
  tenants — upsert-then-prune **per tenant**, so cross-tenant state stays isolated.
- **Validation.** One live host on Postgres `--change-feed`; a tenant-aware peer writer persists stubs for
  two non-default tenants (`acme`, `globex`). The host reloads all tenants and serves each under its
  `X-Mockifyr-Tenant` header; emptying `acme` prunes it while `globex` is untouched. (Admin tenant
  resolution is still a placeholder—default tenant—so non-default tenants are written via the persistence
  seam, which is how a tenant-aware peer would.) Coherence infrastructure, so no oracle.
- **Regression case:** `G16gMultiTenantReloadTests.Reload_Reconciles_All_Tenants_Independently`.
