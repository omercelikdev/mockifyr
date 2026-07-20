using Mediant.Abstractions;
using Mediant.Results;
using Mockifyr.Core;

namespace Mockifyr.Application;

// The management-path CQRS contracts (Mediant). Every operation is tenant-scoped — there is no
// tenant-less overload, mirroring the store contracts. The mock-serving hot path never comes here.

/// <summary>Creates a single stub from the imported stub-mapping JSON; returns its id.</summary>
public sealed record CreateStubCommand(string MappingJson, TenantId Tenant) : ICommand<Result<Guid>>;

/// <summary>Replaces the stub at <paramref name="Id"/> with the given stub-mapping JSON (the <c>PUT /__admin/mappings/{id}</c> admin endpoint).</summary>
public sealed record UpdateStubCommand(Guid Id, string MappingJson, TenantId Tenant) : ICommand<Result>;

/// <summary>Deletes a stub by id.</summary>
public sealed record DeleteStubCommand(Guid Id, TenantId Tenant) : ICommand<Result>;

/// <summary>Imports one or more mappings (a single stub or a <c>{"mappings":[…]}</c> bundle); returns the count.</summary>
public sealed record ImportMappingsCommand(string MappingJson, TenantId Tenant) : ICommand<Result<int>>;

/// <summary>Removes all stubs for the tenant (the <c>/__admin/mappings/reset</c> admin endpoint).</summary>
public sealed record ResetMappingsCommand(TenantId Tenant) : ICommand<Result>;

/// <summary>Lists all stubs for the tenant.</summary>
public sealed record GetStubsQuery(TenantId Tenant) : IQuery<Result<IReadOnlyList<StubMapping>>>;

/// <summary>Gets a single stub by id (<see cref="Error.NotFound"/> when absent).</summary>
public sealed record GetStubQuery(Guid Id, TenantId Tenant) : IQuery<Result<StubMapping>>;

/// <summary>Counts journaled requests matching a request-pattern JSON (verification).</summary>
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

/// <summary>Sets a scenario's state directly (the <c>PUT /__admin/scenarios/{name}/state</c> admin endpoint).</summary>
public sealed record SetScenarioStateCommand(string Name, string State, TenantId Tenant) : ICommand<Result>;

/// <summary>Resets every scenario to <c>Started</c>.</summary>
public sealed record ResetScenariosCommand(TenantId Tenant) : ICommand<Result>;

// Environment keys (G17, issues #165/#166). Every operation carries the tenant: environments are
// tenant-owned, and cross-tenant access must be impossible at the API level, not merely hidden in
// the dashboard.

/// <summary>Lists the tenant's environment keys with their values and which one is active.</summary>
public sealed record GetEnvironmentsQuery(TenantId Tenant) : IQuery<Result<IReadOnlyList<EnvironmentKey>>>;

/// <summary>
/// Creates or replaces an environment key (<c>PUT /__admin/environments/{key}</c>). Rejects a key that
/// is malformed or collides with a built-in templating helper.
/// </summary>
public sealed record PutEnvironmentKeyCommand(EnvironmentKey Key, TenantId Tenant) : ICommand<Result>;

/// <summary>Selects which value is active for a key (<c>PUT /__admin/environments/{key}/active</c>).</summary>
public sealed record SetEnvironmentActiveValueCommand(string Key, string ActiveValue, TenantId Tenant) : ICommand<Result>;

/// <summary>Deletes an environment key (<c>DELETE /__admin/environments/{key}</c>).</summary>
public sealed record DeleteEnvironmentKeyCommand(string Key, TenantId Tenant) : ICommand<Result>;

/// <summary>Deletes every environment key owned by the tenant.</summary>
public sealed record ResetEnvironmentsCommand(TenantId Tenant) : ICommand<Result>;
