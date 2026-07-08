using System.Collections.Concurrent;
using Mockifyr.Core;

namespace Mockifyr.Stores.InMemory;

/// <summary>
/// Tenant-scoped in-memory scenario state store. Unknown scenarios default to
/// the <see cref="Started"/> state (the canonical <c>Started</c> starting point);
/// verified by the differential suite.
/// </summary>
public sealed class InMemoryScenarioStateStore : IScenarioStateStore
{
    /// <summary>The initial state of every scenario.</summary>
    public const string Started = "Started";

    private readonly ConcurrentDictionary<(TenantId, string), string> _states = new();

    /// <inheritdoc />
    public string GetState(TenantId tenant, string scenario) =>
        _states.TryGetValue((tenant, scenario), out var state) ? state : Started;

    /// <inheritdoc />
    public void SetState(TenantId tenant, string scenario, string state) =>
        _states[(tenant, scenario)] = state;
}
