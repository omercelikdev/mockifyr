using Mediant.Abstractions;
using Mediant.Results;
using Mockifyr.Core;

namespace Mockifyr.Application;

// The management-path CQRS contracts (Mediant). Every operation is tenant-scoped — there is no
// tenant-less overload, mirroring the store contracts. The mock-serving hot path never comes here.

/// <summary>Creates a single stub from WireMock JSON; returns its id.</summary>
public sealed record CreateStubCommand(string WireMockJson, TenantId Tenant) : ICommand<Result<Guid>>;

/// <summary>Deletes a stub by id.</summary>
public sealed record DeleteStubCommand(Guid Id, TenantId Tenant) : ICommand<Result>;

/// <summary>Imports one or more mappings (a single stub or a <c>{"mappings":[…]}</c> bundle); returns the count.</summary>
public sealed record ImportMappingsCommand(string WireMockJson, TenantId Tenant) : ICommand<Result<int>>;

/// <summary>Removes all stubs for the tenant (WireMock's <c>/__admin/mappings/reset</c>).</summary>
public sealed record ResetMappingsCommand(TenantId Tenant) : ICommand<Result>;

/// <summary>Lists all stubs for the tenant.</summary>
public sealed record GetStubsQuery(TenantId Tenant) : IQuery<Result<IReadOnlyList<StubMapping>>>;

/// <summary>Gets a single stub by id (<see cref="Error.NotFound"/> when absent).</summary>
public sealed record GetStubQuery(Guid Id, TenantId Tenant) : IQuery<Result<StubMapping>>;

/// <summary>Counts journaled requests matching a WireMock request-pattern JSON (verification).</summary>
public sealed record CountRequestsQuery(string PatternJson, TenantId Tenant) : IQuery<Result<int>>;

/// <summary>Lists the journaled requests that matched no stub.</summary>
public sealed record FindUnmatchedRequestsQuery(TenantId Tenant) : IQuery<Result<IReadOnlyList<CanonicalRequest>>>;

/// <summary>Lists the journaled serve events for a tenant (the request log).</summary>
public sealed record GetServeEventsQuery(TenantId Tenant, bool UnmatchedOnly = false, int? Limit = null)
    : IQuery<Result<IReadOnlyList<ServeEvent>>>;

/// <summary>A scenario's current state and the states it can be in (G12c admin).</summary>
public sealed record ScenarioView(string Name, string State, IReadOnlyList<string> PossibleStates);

/// <summary>Lists the tenant's scenarios with their current state.</summary>
public sealed record GetScenariosQuery(TenantId Tenant) : IQuery<Result<IReadOnlyList<ScenarioView>>>;

/// <summary>Sets a scenario's state directly (WireMock's <c>PUT /__admin/scenarios/{name}/state</c>).</summary>
public sealed record SetScenarioStateCommand(string Name, string State, TenantId Tenant) : ICommand<Result>;

/// <summary>Resets every scenario to <c>Started</c>.</summary>
public sealed record ResetScenariosCommand(TenantId Tenant) : ICommand<Result>;
