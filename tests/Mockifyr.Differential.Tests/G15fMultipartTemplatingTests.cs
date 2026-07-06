using System.Text;
using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of multipart <c>request.parts</c> response templating (G15f). A stub matches
/// a <c>multipart/form-data</c> POST and its <c>response-template</c> body reaches into the parsed parts —
/// each part's <c>body</c>, its <c>name</c>, and a part header — via
/// <c>{{request.parts.&lt;name&gt;.body}}</c> / <c>{{request.parts.&lt;name&gt;.headers.[Content-Type]}}</c>.
/// The oracle (Java WireMock) and Mockifyr must render the <em>same</em> body from the same upload, which
/// is what proves Mockifyr's part model matches WireMock's. Requires Docker.
/// </summary>
public sealed class G15fMultipartTemplatingTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    private const string Boundary = "----MockifyrBoundary7X";

    private static readonly string Body = string.Join("\r\n",
        "--" + Boundary,
        "Content-Disposition: form-data; name=\"meta\"",
        "Content-Type: text/plain",
        "",
        "hello-from-meta",
        "--" + Boundary,
        "Content-Disposition: form-data; name=\"avatar\"; filename=\"a.bin\"",
        "Content-Type: application/octet-stream",
        "",
        "avatar-body-bytes",
        "--" + Boundary + "--",
        "");

    // The template reads three facets of the parsed parts: a part body, a part header (bracket-literal
    // notation for the hyphenated name), and the part's own name — each on both sides.
    private const string MappingJson =
        """
        {
          "request": { "method": "POST", "urlPath": "/upload" },
          "response": {
            "status": 200,
            "transformers": ["response-template"],
            "body": "meta=[{{request.parts.meta.body}}]|metaCt=[{{request.parts.meta.headers.[Content-Type]}}]|avatar=[{{request.parts.avatar.body}}]|avatarName=[{{request.parts.avatar.name}}]"
          }
        }
        """;

    [Fact]
    public async Task MultipartParts_ResponseTemplate_MatchesTheOracle()
    {
        await _runner.LoadAsync(MappingJson);

        var request = new RequestSpec
        {
            Method = "POST",
            Url = "/upload",
            Headers = [new("Content-Type", $"multipart/form-data; boundary={Boundary}")],
            Body = Encoding.UTF8.GetBytes(Body),
        };

        var outcome = await _runner.ProbeAsync(request);

        Assert.True(outcome.OracleMatched, $"oracle did not serve the stub (status {outcome.Oracle.Status})");
        // The oracle proves what the rendered body should be; Mockifyr must match it byte-for-byte.
        Assert.Equal(
            "meta=[hello-from-meta]|metaCt=[text/plain]|avatar=[avatar-body-bytes]|avatarName=[avatar]",
            outcome.Oracle.BodyAsText);
        Assert.True(outcome.Diff.IsMatch, $"response diff — {outcome.Diff.Report}");
    }
}
