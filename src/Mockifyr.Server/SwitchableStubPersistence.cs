using Mockifyr.Core;

namespace Mockifyr.Server;

/// <summary>
/// The pure-in-memory host's persistence seam for a dashboard Git connect (#151): a no-op until
/// <see cref="Activate"/> swaps in file persistence, at which point every subsequent admin mutation
/// writes through to the working copy — exactly the behavior of a <c>--root-dir</c> host. The swap
/// happens once, under the Git service's operation gate; reads of <see cref="_inner"/> are volatile
/// so in-flight admin calls observe it immediately.
/// </summary>
public sealed class SwitchableStubPersistence : IStubPersistence
{
    private volatile IStubPersistence _inner = new NullStubPersistence();

    /// <summary>Whether a real persistence has been activated (connect happened).</summary>
    public bool IsActive => _inner is not NullStubPersistence;

    /// <summary>Swaps the no-op for a real persistence; the caller has already snapshotted the store.</summary>
    public void Activate(IStubPersistence inner) => _inner = inner;

    /// <inheritdoc />
    public void Save(StubMapping stub, string mappingJson) => _inner.Save(stub, mappingJson);

    /// <inheritdoc />
    public void Remove(TenantId tenant, Guid id) => _inner.Remove(tenant, id);

    /// <inheritdoc />
    public void Clear(TenantId tenant) => _inner.Clear(tenant);
}
