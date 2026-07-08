# 0004 — Own domain model + WireMock JSON import adapter

**Status:** Accepted · **Date:** 2026-07-01

## Context
Adopting WireMock's mappings JSON schema directly as our internal model would simplify the
harness but bind us to WireMock's schema choices and weaken the IP. At the same time, the
differential harness must load the same stub into both us and the oracle.

## Decision
We keep our **own clean internal domain model**; WireMock JSON is translated by an **import
adapter** (`Mockifyr.Adapters.MappingJson`). In the differential harness the **single source
of truth is WireMock JSON**: loaded raw into Java WireMock, translated into Mockifyr through
the import adapter. This puts the adapter itself under test and lets the oracle receive the
untouched canonical format.

## Consequences
- (+) Clean, independent IP; the internal model is not a hostage of WireMock's schema.
- (+) The import adapter is automatically validated by the differential suite.
- (+) The admin API and the harness share the same adapter.
- (−) A model ↔ WireMock JSON translation layer must be maintained.
