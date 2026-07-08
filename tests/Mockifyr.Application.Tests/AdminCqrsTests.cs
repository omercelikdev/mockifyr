using Mediant.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Mockifyr.Application;
using Mockifyr.Core;
using Mockifyr.Server;

namespace Mockifyr.Application.Tests;

/// <summary>
/// In-process validation of the G7 management-path CQRS handlers (Mediant): commands and queries
/// dispatched through <see cref="ISender"/> operate on the shared stores that the serving engine
/// also uses. The HTTP admin surface + its differential validation are G7b.
/// </summary>
public sealed class AdminCqrsTests
{
    private static readonly TenantId Tenant = TenantId.Default;

    private static (ISender Sender, StubEngine Engine) Compose()
    {
        var provider = new ServiceCollection().AddMockifyr().BuildServiceProvider();
        return (provider.GetRequiredService<ISender>(), provider.GetRequiredService<StubEngine>());
    }

    [Fact]
    public async Task Create_Get_List_Delete_RoundTrips()
    {
        var (sender, _) = Compose();

        var created = await sender.Send(new CreateStubCommand(
            """{"request":{"method":"GET","url":"/a"},"response":{"status":200,"body":"one"}}""", Tenant));
        Assert.True(created.IsSuccess);

        var byId = await sender.Send(new GetStubQuery(created.Value, Tenant));
        Assert.True(byId.IsSuccess);
        Assert.Equal(created.Value, byId.Value.Id);

        var listed = await sender.Send(new GetStubsQuery(Tenant));
        Assert.Single(listed.Value);

        var deleted = await sender.Send(new DeleteStubCommand(created.Value, Tenant));
        Assert.True(deleted.IsSuccess);
        Assert.Empty((await sender.Send(new GetStubsQuery(Tenant))).Value);
    }

    [Fact]
    public async Task Update_ReplacesInPlace_AndIsServed()
    {
        var (sender, engine) = Compose();

        var created = await sender.Send(new CreateStubCommand(
            """{"request":{"method":"GET","url":"/u"},"response":{"status":200,"body":"before"}}""", Tenant));
        Assert.True(created.IsSuccess);

        var updated = await sender.Send(new UpdateStubCommand(
            created.Value,
            """{"request":{"method":"GET","url":"/u"},"response":{"status":503,"body":"after"}}""",
            Tenant));
        Assert.True(updated.IsSuccess);

        // No duplicate — the update replaced the stub in place under the same id.
        Assert.Single((await sender.Send(new GetStubsQuery(Tenant))).Value);
        Assert.Equal(created.Value, (await sender.Send(new GetStubQuery(created.Value, Tenant))).Value.Id);

        // The serving path reflects the new response.
        var served = engine.Handle(Tenant, CanonicalRequestBuilder.Build("GET", "/u", [], null));
        Assert.True(served.Matched);
        Assert.Equal(503, served.Response!.Status);
    }

    [Fact]
    public async Task Update_MalformedJson_ReturnsValidationError()
    {
        var (sender, _) = Compose();

        var result = await sender.Send(new UpdateStubCommand(Guid.NewGuid(), "{ not json", Tenant));

        Assert.True(result.IsFailure);
        Assert.Equal("Stub.Invalid", result.Error.Code);
    }

    [Fact]
    public async Task GetStub_Missing_ReturnsNotFound()
    {
        var (sender, _) = Compose();

        var result = await sender.Send(new GetStubQuery(Guid.NewGuid(), Tenant));

        Assert.True(result.IsFailure);
        Assert.Equal("Stub.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task Import_Then_Reset()
    {
        var (sender, _) = Compose();

        var imported = await sender.Send(new ImportMappingsCommand(
            """
            {"mappings":[
              {"request":{"method":"GET","url":"/a"},"response":{"status":200}},
              {"request":{"method":"GET","url":"/b"},"response":{"status":200}}
            ]}
            """, Tenant));
        Assert.Equal(2, imported.Value);

        await sender.Send(new ResetMappingsCommand(Tenant));
        Assert.Empty((await sender.Send(new GetStubsQuery(Tenant))).Value);
    }

    [Fact]
    public async Task CreatedStub_IsServedByTheEngine_AndCounted()
    {
        var (sender, engine) = Compose();

        await sender.Send(new CreateStubCommand(
            """{"request":{"method":"POST","url":"/api"},"response":{"status":200,"body":"served"}}""", Tenant));

        // The management path and the serving path share the store, so the engine serves it.
        var matched = engine.Handle(Tenant, CanonicalRequestBuilder.Build("POST", "/api", [], null));
        Assert.True(matched.Matched);

        engine.Handle(Tenant, CanonicalRequestBuilder.Build("GET", "/nope", [], null)); // unmatched

        var count = await sender.Send(new CountRequestsQuery("""{"method":"POST","url":"/api"}""", Tenant));
        Assert.Equal(1, count.Value);

        var unmatched = await sender.Send(new FindUnmatchedRequestsQuery(Tenant));
        Assert.Single(unmatched.Value);
    }

    [Fact]
    public async Task Metadata_And_ExplicitId_AreParsed()
    {
        var (sender, _) = Compose();
        var id = Guid.NewGuid();
        var json = "{\"id\":\"" + id + "\",\"request\":{\"method\":\"GET\",\"url\":\"/m\"}," +
                   "\"response\":{\"status\":200},\"metadata\":{\"team\":\"payments\"}}";

        var created = await sender.Send(new CreateStubCommand(json, Tenant));

        Assert.Equal(id, created.Value); // the explicit id/uuid is honoured
        var stub = (await sender.Send(new GetStubQuery(id, Tenant))).Value;
        Assert.Equal("payments", stub.Metadata?.Values["team"]);
    }
}
