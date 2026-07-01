# 0001 — Transport-agnostic core engine + thin facades

**Status:** Accepted · **Date:** 2026-07-01

## Context
Mockifyr must run in several shapes: in-process library (tests), standalone HTTP container,
and runtime management via an admin REST API. gRPC/GraphQL/WebSocket will follow. Rewriting
matching/templating logic per transport is unsustainable and inconsistent.

## Decision
The core engine **knows nothing about HTTP, ports, or transport.** It takes
`(TenantId, CanonicalRequest)` and returns a `CanonicalResponse` (plus delay/fault/proxy
directives). **Thin facades** sit on top: Library, HTTP server, Admin REST. Each facade
translates its transport into a `CanonicalRequest`; matching/templating logic **never** leaks
into a facade. gRPC/GraphQL/WebSocket become new facades; the engine does not change.

## Consequences
- (+) One engine feeds all facades; behavior stays consistent.
- (+) A new protocol is a new facade; the core is untouched.
- (+) The engine is pure and deterministic, so it can be differentially tested (see 0002).
- (−) A canonical model (`CanonicalRequest/Response`) must be maintained between transport
  and engine.
- Wire-affecting delivery behaviors (CORS/gzip/chunked) are the facade's responsibility, not
  the engine's.
