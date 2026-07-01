# 0006 — Pluggable persistence; in-memory hot-path source of truth

**Status:** Accepted · **Date:** 2026-07-01

## Context
Stubs must eventually be durable and, ideally, editable out-of-band (e.g. edit a file or a DB
row and have the engine reflect it). A tempting but wrong design is to make the matching hot
path read from a database on every request.

## Decision
The **in-memory compiled index is the single source of truth for the hot path.** A request
never reads the database; matchers are compiled (regex/JSON/JSONPath), so raw DB rows are not
usable on the hot path anyway. Persistence is a provider behind `IStubStore` (+
`IScenarioStateStore`, `IRequestJournal`) that (a) loads at startup, (b) writes through on
changes, and (c) feeds external changes back via an explicit `IStubChangeSource` reload seam.

Providers (Phase B): `InMemory` (default, ephemeral) → `FileBased` (WireMock-style JSON
`mappings/` dir + file watcher — the cleanest answer to "edit directly and have it reflected",
and WireMock-compatible + git-friendly) → `LiteDB` (embedded, single node) → `Postgres`/`Redis`
(shared, multi-replica HA on OpenShift; Postgres `LISTEN/NOTIFY`, Redis pub/sub).

"Update directly from the DB and have the engine reflect it" is served by the reload seam
(file-watch / notify / polling + a manual `POST /__admin/reload`), **not** by reading the DB
on the hot path.

## Consequences
- (+) The hot path stays fast; cache invalidation is solved at one explicit point.
- (+) New provider = new project; Core does not change (contracts exist from day one).
- (−) `LiteDB` is fine for a single node but has file-lock issues under multi-replica → a
  shared provider (Postgres/Redis) is required there.
- Phase A ships only `InMemory`; the contracts are in Core from the start so providers slot in
  later without a refactor.
