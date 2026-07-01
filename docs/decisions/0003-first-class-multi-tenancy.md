# 0003 — First-class multi-tenancy (logical isolation)

**Status:** Accepted · **Date:** 2026-07-01

## Context
A single Mockifyr instance must serve multiple teams/environments. Solving this by spawning
ports is expensive and does not scale. Adding multi-tenancy later is the most expensive path.

## Decision
Multi-tenancy is achieved **not by separate ports but by logical isolation inside the
engine.** One process, one port, N tenants. Every stub / scenario-state / journal entry
belongs to a `TenantId`; matching **always** runs within a tenant scope. Tenant resolution
happens at the transport layer (subdomain / path-prefix / header → `TenantId`); WireMock's
native "multi-domain" feature dissolves into one such resolution strategy.

Isolation is **enforced by the type system, not ambient:** every store/engine entry point
requires a `tenantId`; there is **no** tenant-less API such as `GetAllStubs()`, so forgetting
scope is a compile error. Cross-tenant visibility exists only in a separate, privileged
"system" scope (the UI's tenant selector).

## Consequences
- (+) Leakage is prevented at compile time, not left to runtime.
- (+) In the model from day one → no expensive later refactor.
- (−) WireMock has no tenant concept, so the differential harness tests a single tenant;
  isolation is verified with separate invariant tests (not against the oracle).
