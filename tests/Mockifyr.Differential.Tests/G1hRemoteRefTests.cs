using System.Text;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of remote/URL <c>$ref</c> resolution in <c>matchesJsonSchema</c> (G1h): a
/// schema whose property references a schema hosted over HTTP (<c>$ref</c> to a host-side
/// <see cref="SchemaServer"/>). WireMock/networknt fetches the remote schema; Mockifyr's JsonSchema.Net
/// is taught to as well. The <c>$ref</c> host is rewritten per side (the oracle reaches the server via
/// <c>host.docker.internal</c>, Mockifyr via <c>127.0.0.1</c>). Requires Docker.
/// </summary>
public sealed class G1hRemoteRefTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();
    private readonly SchemaServer _schema = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync()
    {
        _schema.Dispose();
        await _runner.DisposeAsync();
    }

    // The referenced schema (served remotely) requires a `city` string; the top-level schema requires
    // `addr` to satisfy that remote `$ref`.
    private const string StubTemplate =
        """
        {
          "request": {
            "method": "POST",
            "urlPath": "/p",
            "bodyPatterns": [ { "matchesJsonSchema": {
              "type": "object",
              "required": ["addr"],
              "properties": { "addr": { "$ref": "http://__SCHEMA_HOST__/addr.json" } }
            } } ]
          },
          "response": { "status": 200, "body": "ok" }
        }
        """;

    [Fact]
    public async Task RemoteRef_Resolves_AndAgreesWithTheOracle()
    {
        var valid = await _runner.RunSchemaRefAsync(_schema.Port, StubTemplate,
            new RequestSpec { Method = "POST", Url = "/p", Body = Encoding.UTF8.GetBytes("""{"addr":{"city":"NYC"}}""") });
        var invalid = await _runner.RunSchemaRefAsync(_schema.Port, StubTemplate,
            new RequestSpec { Method = "POST", Url = "/p", Body = Encoding.UTF8.GetBytes("""{"addr":{"zip":"10001"}}""") });

        // The remote schema's `required: city` is enforced through the `$ref` on both sides.
        Assert.True(valid.OracleMatched, "oracle should resolve the remote $ref and match");
        Assert.True(valid.DecisionAgrees, $"valid: oracle={valid.OracleMatched} mockifyr={valid.MockifyrMatched}");
        Assert.False(invalid.OracleMatched, "oracle should fail the remote-referenced required city");
        Assert.True(invalid.DecisionAgrees, $"invalid: oracle={invalid.OracleMatched} mockifyr={invalid.MockifyrMatched}");
    }
}
