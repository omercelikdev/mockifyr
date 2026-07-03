using System.Collections.Concurrent;
using Mockifyr.Core;

namespace Mockifyr.Stores.InMemory;

/// <summary>
/// In-memory registry of named custom matchers contributed by extensions (G10). Populated at
/// composition time; read when the adapter resolves a <c>customMatcher</c> reference.
/// </summary>
public sealed class InMemoryMatcherRegistry : IMatcherRegistry
{
    private readonly ConcurrentDictionary<string, IMatcher> _matchers = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Register(string name, IMatcher matcher) => _matchers[name] = matcher;

    /// <inheritdoc />
    public IMatcher? Resolve(string name) => _matchers.GetValueOrDefault(name);
}
