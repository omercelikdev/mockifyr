using System.Linq;
using System.Text.Json;
using Mediant.Abstractions;
using Mediant.Results;
using Mockifyr.Adapters.MappingJson;
using Mockifyr.Core;

namespace Mockifyr.Application;

// Handlers for the management-path operations. They depend only on Core contracts (the stub store)
// and the engine's read-only verification queries; Mediant registers them by assembly scan.

/// <summary>Creates a stub and returns its id, or a validation error if the JSON yields none.</summary>
public sealed class CreateStubHandler(IStubStore store, IMatcherRegistry matchers, IStubPersistence persistence)
    : ICommandHandler<CreateStubCommand, Result<Guid>>
{
    public ValueTask<Result<Guid>> Handle(CreateStubCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<(StubMapping Stub, string Source)> stubs;
        try
        {
            stubs = MappingJsonReader.ReadWithSource(command.MappingJson, command.Tenant, matchers);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            // JsonException = malformed JSON; InvalidOperationException = a well-formed but wrong-typed
            // field (e.g. a string where a numeric status is expected). Both are client input errors.
            return ValueTask.FromResult<Result<Guid>>(Error.Validation("Stub.Invalid", "The stub JSON is malformed."));
        }

        if (stubs.Count == 0)
        {
            return ValueTask.FromResult<Result<Guid>>(Error.Validation("Stub.Invalid", "No stub could be read from the JSON."));
        }

        store.Put(stubs[0].Stub);
        persistence.Save(stubs[0].Stub, stubs[0].Source);
        return ValueTask.FromResult<Result<Guid>>(stubs[0].Stub.Id);
    }
}

/// <summary>
/// Replaces an existing stub via the admin route <c>PUT /__admin/mappings/{id}</c>. The route id is
/// authoritative: the parsed stub's id is forced to it so <see cref="IStubStore.Put"/> upserts in place
/// rather than appending a duplicate. Returns a validation error for malformed/empty JSON, matching create.
/// </summary>
public sealed class UpdateStubHandler(IStubStore store, IMatcherRegistry matchers, IStubPersistence persistence)
    : ICommandHandler<UpdateStubCommand, Result>
{
    public ValueTask<Result> Handle(UpdateStubCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<(StubMapping Stub, string Source)> stubs;
        try
        {
            stubs = MappingJsonReader.ReadWithSource(command.MappingJson, command.Tenant, matchers);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return ValueTask.FromResult(Result.Failure(Error.Validation("Stub.Invalid", "The stub JSON is malformed.")));
        }

        if (stubs.Count == 0)
        {
            return ValueTask.FromResult(Result.Failure(Error.Validation("Stub.Invalid", "No stub could be read from the JSON.")));
        }

        var updated = stubs[0].Stub with { Id = command.Id };
        store.Put(updated);
        persistence.Save(updated, stubs[0].Source);
        return ValueTask.FromResult(Result.Success());
    }
}

/// <summary>Deletes a stub by id; idempotent — deleting a missing stub still succeeds (verified by the differential suite).</summary>
public sealed class DeleteStubHandler(IStubStore store, IStubPersistence persistence)
    : ICommandHandler<DeleteStubCommand, Result>
{
    public ValueTask<Result> Handle(DeleteStubCommand command, CancellationToken cancellationToken)
    {
        store.Remove(command.Tenant, command.Id);
        persistence.Remove(command.Tenant, command.Id);
        return ValueTask.FromResult(Result.Success());
    }
}

/// <summary>Imports one stub or a bundle of them, returning how many were loaded.</summary>
public sealed class ImportMappingsHandler(IStubStore store, IMatcherRegistry matchers, IStubPersistence persistence)
    : ICommandHandler<ImportMappingsCommand, Result<int>>
{
    public ValueTask<Result<int>> Handle(ImportMappingsCommand command, CancellationToken cancellationToken)
    {
        IReadOnlyList<(StubMapping Stub, string Source)> stubs;
        try
        {
            stubs = MappingJsonReader.ReadWithSource(command.MappingJson, command.Tenant, matchers);
        }
        catch (JsonException)
        {
            return ValueTask.FromResult<Result<int>>(Error.Validation("Mappings.Invalid", "The mappings JSON is malformed."));
        }

        foreach (var (stub, source) in stubs)
        {
            store.Put(stub);
            persistence.Save(stub, source);
        }

        return ValueTask.FromResult<Result<int>>(stubs.Count);
    }
}

/// <summary>Removes every stub for the tenant.</summary>
public sealed class ResetMappingsHandler(IStubStore store, IStubPersistence persistence)
    : ICommandHandler<ResetMappingsCommand, Result>
{
    public ValueTask<Result> Handle(ResetMappingsCommand command, CancellationToken cancellationToken)
    {
        foreach (var stub in store.GetStubs(command.Tenant).ToList())
        {
            store.Remove(command.Tenant, stub.Id);
        }

        persistence.Clear(command.Tenant);
        return ValueTask.FromResult(Result.Success());
    }
}

/// <summary>Lists all stubs for the tenant.</summary>
public sealed class GetStubsHandler(IStubStore store) : IQueryHandler<GetStubsQuery, Result<IReadOnlyList<StubMapping>>>
{
    public ValueTask<Result<IReadOnlyList<StubMapping>>> Handle(GetStubsQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(store.GetStubs(query.Tenant)));
}

/// <summary>Gets a single stub by id, or a not-found error.</summary>
public sealed class GetStubHandler(IStubStore store) : IQueryHandler<GetStubQuery, Result<StubMapping>>
{
    public ValueTask<Result<StubMapping>> Handle(GetStubQuery query, CancellationToken cancellationToken)
    {
        var stub = store.GetStubs(query.Tenant).FirstOrDefault(s => s.Id == query.Id);
        return stub is null
            ? ValueTask.FromResult<Result<StubMapping>>(Error.NotFound("Stub.NotFound", $"No stub with id {query.Id}."))
            : ValueTask.FromResult<Result<StubMapping>>(stub);
    }
}

/// <summary>Counts journaled requests matching the given request pattern.</summary>
public sealed class CountRequestsHandler(StubEngine engine) : IQueryHandler<CountRequestsQuery, Result<int>>
{
    public ValueTask<Result<int>> Handle(CountRequestsQuery query, CancellationToken cancellationToken)
    {
        var pattern = MappingJsonReader.ReadRequestPattern(query.PatternJson);
        return ValueTask.FromResult<Result<int>>(engine.CountRequestsMatching(query.Tenant, pattern));
    }
}

/// <summary>Lists the journaled requests that matched no stub.</summary>
public sealed class FindUnmatchedRequestsHandler(StubEngine engine)
    : IQueryHandler<FindUnmatchedRequestsQuery, Result<IReadOnlyList<CanonicalRequest>>>
{
    public ValueTask<Result<IReadOnlyList<CanonicalRequest>>> Handle(
        FindUnmatchedRequestsQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(engine.FindUnmatchedRequests(query.Tenant)));
}

/// <summary>Lists the journaled serve events for a tenant (the request log).</summary>
public sealed class GetServeEventsHandler(StubEngine engine)
    : IQueryHandler<GetServeEventsQuery, Result<IReadOnlyList<ServeEvent>>>
{
    public ValueTask<Result<IReadOnlyList<ServeEvent>>> Handle(
        GetServeEventsQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Result.Success(
            engine.GetServeEvents(query.Tenant, new ServeEventQuery { UnmatchedOnly = query.UnmatchedOnly, Limit = query.Limit })));
}

/// <summary>Projects the tenant's scenarios (from the bound stubs) with their current state.</summary>
public sealed class GetScenariosHandler(IStubStore store, IScenarioStateStore states)
    : IQueryHandler<GetScenariosQuery, Result<IReadOnlyList<ScenarioView>>>
{
    public ValueTask<Result<IReadOnlyList<ScenarioView>>> Handle(GetScenariosQuery query, CancellationToken cancellationToken)
    {
        var scenarios = store.GetStubs(query.Tenant)
            .Where(stub => stub.Scenario is not null)
            .GroupBy(stub => stub.Scenario!.ScenarioName)
            .Select(group => new ScenarioView(
                group.Key,
                states.GetState(query.Tenant, group.Key),
                PossibleStates(group)))
            .ToList();

        return ValueTask.FromResult(Result.Success<IReadOnlyList<ScenarioView>>(scenarios));
    }

    // The default "Started" state plus every state the scenario's stubs require or transition to (verified by the differential suite).
    private static IReadOnlyList<string> PossibleStates(IEnumerable<StubMapping> stubs)
    {
        var states = new HashSet<string>(StringComparer.Ordinal) { "Started" };
        foreach (var stub in stubs)
        {
            if (stub.Scenario!.RequiredState is { } required)
            {
                states.Add(required);
            }

            if (stub.Scenario!.NewState is { } next)
            {
                states.Add(next);
            }
        }

        return [.. states.OrderBy(s => s, StringComparer.Ordinal)];
    }
}

/// <summary>Sets a scenario's state directly.</summary>
public sealed class SetScenarioStateHandler(IScenarioStateStore states) : ICommandHandler<SetScenarioStateCommand, Result>
{
    public ValueTask<Result> Handle(SetScenarioStateCommand command, CancellationToken cancellationToken)
    {
        states.SetState(command.Tenant, command.Name, command.State);
        return ValueTask.FromResult(Result.Success());
    }
}

/// <summary>Resets every scenario for the tenant to <c>Started</c>.</summary>
public sealed class ResetScenariosHandler(IStubStore store, IScenarioStateStore states)
    : ICommandHandler<ResetScenariosCommand, Result>
{
    public ValueTask<Result> Handle(ResetScenariosCommand command, CancellationToken cancellationToken)
    {
        foreach (var name in store.GetStubs(command.Tenant)
                     .Select(stub => stub.Scenario?.ScenarioName)
                     .Where(name => name is not null)
                     .Distinct())
        {
            states.SetState(command.Tenant, name!, "Started");
        }

        return ValueTask.FromResult(Result.Success());
    }
}
