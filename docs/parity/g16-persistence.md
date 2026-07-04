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
