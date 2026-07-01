namespace Mockifyr.Application;

// Placeholder for the Mockifyr.Application assembly.
// CQRS command/query handlers (via Mediant) for the MANAGEMENT path only:
// CreateStub, UpdateStub, DeleteStub, ImportMappings, ResetJournal/Scenarios,
// GetStub, ListStubs, FindRequests, GetNearMisses, ... They return Result<T>.
// The mock-serving hot path never goes through here.
// See docs/decisions/0005-cqrs-mediant-application-layer.md.
