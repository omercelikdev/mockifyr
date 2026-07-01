using System.Collections.Concurrent;
using Mockifyr.Core;

namespace Mockifyr.Stores.InMemory;

/// <summary>
/// Tenant-scoped in-memory request journal. Records serve events and answers simple queries;
/// verification and near-miss diagnostics (G6) build on this.
/// </summary>
public sealed class InMemoryRequestJournal : IRequestJournal
{
    private readonly ConcurrentDictionary<TenantId, List<ServeEvent>> _byTenant = new();
    private readonly Lock _gate = new();

    /// <inheritdoc />
    public void Record(ServeEvent serveEvent)
    {
        lock (_gate)
        {
            var events = _byTenant.GetOrAdd(serveEvent.TenantId, static _ => []);
            events.Add(serveEvent);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ServeEvent> Query(TenantId tenant, ServeEventQuery query)
    {
        lock (_gate)
        {
            if (!_byTenant.TryGetValue(tenant, out var events))
            {
                return [];
            }

            IEnumerable<ServeEvent> result = events;

            if (query.UnmatchedOnly)
            {
                result = result.Where(e => e.MatchedStub is null);
            }

            if (query.MatchingStubId is { } stubId)
            {
                result = result.Where(e => e.MatchedStub?.Id == stubId);
            }

            if (query.Limit is { } limit)
            {
                result = result.Take(limit);
            }

            return [.. result];
        }
    }
}
