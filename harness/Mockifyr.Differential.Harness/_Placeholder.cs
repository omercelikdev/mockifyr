namespace Mockifyr.Differential.Harness;

// Placeholder for the differential harness.
// Brings up real Java WireMock (Testcontainers), loads WireMock JSON raw into the oracle
// and via the import adapter into Mockifyr, drives the same request through both, then
// canonicalizes + volatile-masks and diffs the responses. Built at G0.
// Testcontainers is added as a package reference when this logic is implemented.
// See docs/decisions/0002-differential-oracle-wiremock.md and ARCHITECTURE.md section 11.
