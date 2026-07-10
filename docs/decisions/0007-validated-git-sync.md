# 0007 — Validated Git sync over the root-dir working copy

**Status:** Accepted · **Date:** 2026-07-10

## Context

Teams share one live host and move stub sets between environments by export/import. That flow has
no history, no rollback, and no review — a silent corruption (issue #142) is unrecoverable, and a
restart of an in-memory host loses everything. Issue #143 asks for stub data in Git with push/pull.
Hard requirements from the maintainer: **no half-measures** — the sync must be validated and must
never break a running host with bad data; it must work against **any** plain Git remote (GitHub,
GitLab, Bitbucket, self-hosted) including private repos.

## Decision

**The Git working copy IS the existing `--root-dir`.** File persistence (ADR 0006, G16a) already
writes every stub as a mapping JSON under `<root-dir>/mappings` and reloads it on startup, so Git
integration is a thin sync layer, not a new persistence backend:

- **Plain `git` CLI, no provider APIs.** The host shells out to the `git` binary
  (`ProcessStartInfo.ArgumentList`, never a shell), which makes every Git host work identically —
  HTTPS or SSH, cloud or self-hosted. No GitHub/GitLab SDKs, no new NuGet dependencies.
- **Credentials never touch disk, argv, or logs.** HTTPS tokens are read by an inline
  `credential.helper` from the host's environment (`MOCKIFYR_GIT_TOKEN`, optional
  `MOCKIFYR_GIT_USERNAME` for Bitbucket app passwords); SSH uses ambient keys.
  `GIT_TERMINAL_PROMPT=0` guarantees no hang on a credential prompt, and every surfaced error
  message is scrubbed of the token value.
- **Pull validates before it applies — all or nothing.** The fetched tree's mapping files are read
  straight from the Git objects (`git show FETCH_HEAD:<path>`) and parsed with the same strict
  `MappingJsonReader` the admin API uses. One invalid file fails the whole pull with a per-file
  error report and **nothing changes** — not the working tree, not the served stubs. Only a fully
  valid tree is fast-forwarded and then reconciled into the store via the change-feed reconciler
  (upsert-then-prune, no empty window).
- **Fast-forward only; conflicts are refused, never resolved.** Push checks the remote **before**
  committing, so a "pull first" refusal leaves the working copy untouched. Pull fast-forwards and
  leans on git's own no-clobber guarantee: non-overlapping local edits survive; an update that
  would touch a locally modified file is refused as an explicit overlap ("push first"). Divergent
  histories are reported for resolution outside the host. No merge machinery.
- **Explicit sync only.** Startup never pulls; `POST /__admin/git/push`, `POST /__admin/git/pull`,
  and `GET /__admin/git/status` are the whole surface (thin Admin routes → Mediant handlers →
  `IGitSync`). Repo initialization (init / remote add) is lazy and idempotent on first use.
- **Placement.** The `IGitSync` contract + CQRS operations live in `Mockifyr.Application`
  (management path); the implementation (`GitSyncService`, process + file I/O) lives at the host
  edge in `Mockifyr.Server`, enabled by `--git-remote <url>` (+ optional `--git-branch`, default
  `main`), which requires `--root-dir`. Core is untouched — the engine has no Git concept.

## Validation

WireMock has no Git surface, so there is no oracle to diff; like the other oracle-less features
(Faker/JWT/WebSocket) this is covered by self-tests against a local bare repository: push→pull
round-trip between two hosts serving identical stubs, wholesale rejection of an invalid remote
tree, non-fast-forward refusal, dirty-tree refusal, and token-scrubbing of error output.

## Consequences

- (+) Works with every Git provider by construction; private repos via env-held token or SSH.
- (+) A broken or malicious remote tree can never take down or corrupt a running host.
- (+) History/rollback/review for stub sets with zero new dependencies.
- (−) The `git` binary becomes a host requirement **when the flag is used** (added to the Docker
  image); hosts without `--git-remote` are unaffected.
- (−) No automatic conflict resolution — by design; divergence is resolved in Git tooling.
- (−) Startup reload of non-default tenants still follows the existing loader behavior (default
  tenant only), unchanged by this ADR.
