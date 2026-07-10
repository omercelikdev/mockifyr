using Mediant.Abstractions;
using Mediant.Results;

namespace Mockifyr.Application;

// Git sync (ADR 0007): explicit, validated push/pull of the host's root-dir working copy. The
// contract lives here (management path); the implementation is a host-edge service registered by
// the standalone host when --git-remote is configured. The engine has no Git concept.

/// <summary>
/// The Git sync seam: status/push/pull over the root-dir working copy. Implementations must be
/// safe to call concurrently (operations serialize internally) and must never surface credentials
/// in results or error messages.
/// </summary>
public interface IGitSync
{
    Task<Result<GitSyncStatus>> StatusAsync(CancellationToken cancellationToken);
    Task<Result<GitPushOutcome>> PushAsync(string? message, CancellationToken cancellationToken);
    Task<Result<GitPullOutcome>> PullAsync(CancellationToken cancellationToken);
    Task<Result<GitSyncStatus>> ConfigureAsync(string remoteUrl, string? branch, CancellationToken cancellationToken);
}

/// <summary>
/// Sync state for the dashboard: remote/branch (credentials stripped), local changes, ahead/behind
/// counts, plus where the configuration came from — <c>flags</c> (host-pinned, read-only in the UI)
/// or <c>repository</c> (set from the dashboard, stored in the working copy's own .git/config).
/// </summary>
public sealed record GitSyncStatus(
    bool Configured, string? Remote, string? Branch, bool Dirty, int Ahead, int Behind, string? FetchError,
    string? ConfiguredBy = null, string? WorkingCopy = null);

/// <summary>Push result. <c>Reason</c> is <c>pushed</c> or <c>nothing-to-push</c>.</summary>
public sealed record GitPushOutcome(bool Pushed, string? Commit, string Reason);

/// <summary>Pull result. <c>Reason</c> is <c>fast-forwarded</c> or <c>up-to-date</c>.</summary>
public sealed record GitPullOutcome(bool Updated, string? Commit, int StubsLoaded, string Reason);

/// <summary>Reports the sync state (never mutates; the fetch it performs is read-only).</summary>
public sealed record GitStatusQuery : IQuery<Result<GitSyncStatus>>;

/// <summary>Commits every working-copy change and pushes the branch; refuses when the remote is ahead.</summary>
public sealed record GitPushCommand(string? Message) : ICommand<Result<GitPushOutcome>>;

/// <summary>Fast-forwards to the remote branch after validating every incoming mapping file; all-or-nothing.</summary>
public sealed record GitPullCommand : ICommand<Result<GitPullOutcome>>;

/// <summary>
/// Connects the host's working copy to a Git remote from the dashboard (#151). Refused on hosts
/// whose configuration is pinned by <c>--git-remote</c>. The branch defaults to <c>main</c>.
/// </summary>
public sealed record GitConfigureCommand(string RemoteUrl, string? Branch) : ICommand<Result<GitSyncStatus>>;

/// <summary>
/// The default when no <c>--git-remote</c> is configured: status reports unconfigured (so the
/// dashboard can hide/disable the controls), mutations fail with <c>Git.NotConfigured</c>.
/// </summary>
public sealed class NotConfiguredGitSync : IGitSync
{
    private static readonly Error NotConfigured = Error.NotFound(
        "Git.NotConfigured", "Git sync is not configured. Start the host with --git-remote (and --root-dir).");

    public Task<Result<GitSyncStatus>> StatusAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(new GitSyncStatus(false, null, null, false, 0, 0, null)));

    public Task<Result<GitPushOutcome>> PushAsync(string? message, CancellationToken cancellationToken) =>
        Task.FromResult<Result<GitPushOutcome>>(NotConfigured);

    public Task<Result<GitPullOutcome>> PullAsync(CancellationToken cancellationToken) =>
        Task.FromResult<Result<GitPullOutcome>>(NotConfigured);

    public Task<Result<GitSyncStatus>> ConfigureAsync(string remoteUrl, string? branch, CancellationToken cancellationToken) =>
        Task.FromResult<Result<GitSyncStatus>>(Error.NotFound(
            "Git.NotSupported", "This host was composed without Git sync support."));
}

/// <summary>Delegates the status query to the configured <see cref="IGitSync"/>.</summary>
public sealed class GitStatusHandler(IGitSync git) : IQueryHandler<GitStatusQuery, Result<GitSyncStatus>>
{
    public async ValueTask<Result<GitSyncStatus>> Handle(GitStatusQuery query, CancellationToken cancellationToken) =>
        await git.StatusAsync(cancellationToken);
}

/// <summary>Delegates the push command to the configured <see cref="IGitSync"/>.</summary>
public sealed class GitPushHandler(IGitSync git) : ICommandHandler<GitPushCommand, Result<GitPushOutcome>>
{
    public async ValueTask<Result<GitPushOutcome>> Handle(GitPushCommand command, CancellationToken cancellationToken) =>
        await git.PushAsync(command.Message, cancellationToken);
}

/// <summary>Delegates the pull command to the configured <see cref="IGitSync"/>.</summary>
public sealed class GitPullHandler(IGitSync git) : ICommandHandler<GitPullCommand, Result<GitPullOutcome>>
{
    public async ValueTask<Result<GitPullOutcome>> Handle(GitPullCommand command, CancellationToken cancellationToken) =>
        await git.PullAsync(cancellationToken);
}

/// <summary>Delegates the configure command to the configured <see cref="IGitSync"/>.</summary>
public sealed class GitConfigureHandler(IGitSync git) : ICommandHandler<GitConfigureCommand, Result<GitSyncStatus>>
{
    public async ValueTask<Result<GitSyncStatus>> Handle(GitConfigureCommand command, CancellationToken cancellationToken) =>
        await git.ConfigureAsync(command.RemoteUrl, command.Branch, cancellationToken);
}
