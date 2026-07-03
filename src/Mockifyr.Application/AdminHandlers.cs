using System.Linq;
using Mediant.Abstractions;
using Mediant.Results;
using Mockifyr.Adapters.WireMockJson;
using Mockifyr.Core;

namespace Mockifyr.Application;

// Handlers for the management-path operations. They depend only on Core contracts (the stub store)
// and the engine's read-only verification queries; Mediant registers them by assembly scan.

/// <summary>Creates a stub and returns its id, or a validation error if the JSON yields none.</summary>
public sealed class CreateStubHandler(IStubStore store) : ICommandHandler<CreateStubCommand, Result<Guid>>
{
    public ValueTask<Result<Guid>> Handle(CreateStubCommand command, CancellationToken cancellationToken)
    {
        var stubs = WireMockMappingReader.Read(command.WireMockJson, command.Tenant);
        if (stubs.Count == 0)
        {
            return ValueTask.FromResult<Result<Guid>>(Error.Validation("Stub.Invalid", "No stub could be read from the JSON."));
        }

        store.Put(stubs[0]);
        return ValueTask.FromResult<Result<Guid>>(stubs[0].Id);
    }
}

/// <summary>Deletes a stub by id (idempotent — deleting a missing stub still succeeds, like WireMock).</summary>
public sealed class DeleteStubHandler(IStubStore store) : ICommandHandler<DeleteStubCommand, Result>
{
    public ValueTask<Result> Handle(DeleteStubCommand command, CancellationToken cancellationToken)
    {
        store.Remove(command.Tenant, command.Id);
        return ValueTask.FromResult(Result.Success());
    }
}

/// <summary>Imports one stub or a bundle of them, returning how many were loaded.</summary>
public sealed class ImportMappingsHandler(IStubStore store) : ICommandHandler<ImportMappingsCommand, Result<int>>
{
    public ValueTask<Result<int>> Handle(ImportMappingsCommand command, CancellationToken cancellationToken)
    {
        var stubs = WireMockMappingReader.Read(command.WireMockJson, command.Tenant);
        foreach (var stub in stubs)
        {
            store.Put(stub);
        }

        return ValueTask.FromResult<Result<int>>(stubs.Count);
    }
}

/// <summary>Removes every stub for the tenant.</summary>
public sealed class ResetMappingsHandler(IStubStore store) : ICommandHandler<ResetMappingsCommand, Result>
{
    public ValueTask<Result> Handle(ResetMappingsCommand command, CancellationToken cancellationToken)
    {
        foreach (var stub in store.GetStubs(command.Tenant).ToList())
        {
            store.Remove(command.Tenant, stub.Id);
        }

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
        var pattern = WireMockMappingReader.ReadRequestPattern(query.PatternJson);
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
