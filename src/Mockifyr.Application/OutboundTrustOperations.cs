using Mediant.Abstractions;
using Mediant.Results;

namespace Mockifyr.Application;

/// <summary>
/// The state of outbound certificate trust (#174). Host-level, deliberately <b>not</b> tenant-scoped:
/// the outbound <c>HttpClient</c> is shared by every tenant, so one tenant trusting a host would
/// silently change what every other tenant's callbacks and proxies accept.
/// </summary>
/// <param name="Hosts">Hosts whose certificate is accepted even when the chain does not validate.</param>
/// <param name="TrustAll">Whether verification is disabled outright (flag-only, never settable here).</param>
/// <param name="Pinned">
/// True when a <c>--trust-*</c> flag fixed the configuration at startup, making it read-only —
/// the same two-mode design Git sync uses (ADR 0007).
/// </param>
/// <param name="Persistent">
/// Whether changes survive a restart. False on a host with no root directory, where there is nowhere
/// to write; surfaced so the dashboard can say so rather than implying durability it does not have.
/// </param>
public sealed record OutboundTrustStatus(
    IReadOnlyList<string> Hosts,
    bool TrustAll,
    bool Pinned,
    bool Persistent);

/// <summary>
/// Runtime management of outbound certificate trust. The implementation lives at the host edge (it
/// touches the filesystem); the Application layer depends only on this contract.
/// </summary>
public interface IOutboundTrust
{
    /// <summary>The current state.</summary>
    OutboundTrustStatus Status();

    /// <summary>Trusts a host's certificate. Fails when the configuration is flag-pinned.</summary>
    Result<OutboundTrustStatus> Trust(string host);

    /// <summary>Stops trusting a host. Fails when the configuration is flag-pinned.</summary>
    Result<OutboundTrustStatus> Distrust(string host);
}

/// <summary>
/// The default when no host wiring supplies one: nothing is trusted and nothing can be changed. Keeps
/// the admin API answering rather than failing to resolve a dependency.
/// </summary>
public sealed class NoOutboundTrust : IOutboundTrust
{
    /// <inheritdoc />
    public OutboundTrustStatus Status() => new([], TrustAll: false, Pinned: false, Persistent: false);

    /// <inheritdoc />
    public Result<OutboundTrustStatus> Trust(string host) =>
        Error.Failure("Trust.Unavailable", "This host does not support managing outbound trust.");

    /// <inheritdoc />
    public Result<OutboundTrustStatus> Distrust(string host) => Trust(host);
}

/// <summary>Reads the current outbound trust configuration.</summary>
public sealed record OutboundTrustQuery : IQuery<Result<OutboundTrustStatus>>;

/// <summary>Trusts a host's certificate (<c>POST /__admin/outbound-trust/hosts</c>).</summary>
public sealed record TrustHostCommand(string Host) : ICommand<Result<OutboundTrustStatus>>;

/// <summary>Stops trusting a host (<c>DELETE /__admin/outbound-trust/hosts/{host}</c>).</summary>
public sealed record DistrustHostCommand(string Host) : ICommand<Result<OutboundTrustStatus>>;

/// <summary>Reads the trust configuration.</summary>
public sealed class OutboundTrustHandler(IOutboundTrust trust)
    : IQueryHandler<OutboundTrustQuery, Result<OutboundTrustStatus>>
{
    public ValueTask<Result<OutboundTrustStatus>> Handle(OutboundTrustQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(trust.Status()));
}

/// <summary>Trusts a host.</summary>
public sealed class TrustHostHandler(IOutboundTrust trust)
    : ICommandHandler<TrustHostCommand, Result<OutboundTrustStatus>>
{
    public ValueTask<Result<OutboundTrustStatus>> Handle(TrustHostCommand command, CancellationToken cancellationToken) =>
        ValueTask.FromResult(trust.Trust(command.Host));
}

/// <summary>Stops trusting a host.</summary>
public sealed class DistrustHostHandler(IOutboundTrust trust)
    : ICommandHandler<DistrustHostCommand, Result<OutboundTrustStatus>>
{
    public ValueTask<Result<OutboundTrustStatus>> Handle(DistrustHostCommand command, CancellationToken cancellationToken) =>
        ValueTask.FromResult(trust.Distrust(command.Host));
}
