using Mockifyr.Core;
using Mockifyr.Facade.Library;

namespace Mockifyr.Differential.Harness;

/// <summary>
/// Drives Mockifyr in-process (via the library facade) so its output can be diffed against the
/// oracle. The engine is transport-agnostic, so an in-process drive is a faithful test of its
/// response semantics; wire-level facade behaviors are validated separately from G12.
/// </summary>
public sealed class MockifyrUnderTest
{
    private readonly MockifyrServer _server = new();

    /// <summary>Imports the same WireMock JSON the oracle receives.</summary>
    public void ImportWireMockJson(string wireMockJson) => _server.ImportWireMockJson(wireMockJson);

    /// <summary>Handles a request and snapshots the response.</summary>
    public HttpResponseSnapshot Send(RequestSpec spec)
    {
        var request = CanonicalRequestBuilder.Build(spec.Method, spec.Url, spec.Headers, spec.Body);
        var resolution = _server.Handle(request);

        if (!resolution.Matched || resolution.Response is not { } response)
        {
            // No stub matched. WireMock serves a 404 here; the full no-match body is compared
            // from the group that implements it. For now report a bare 404.
            return new HttpResponseSnapshot
            {
                Status = 404,
                Headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase),
                Body = [],
            };
        }

        var headers = response.Headers.ToDictionary(
            g => g.Key,
            g => g.ToArray(),
            StringComparer.OrdinalIgnoreCase);

        return new HttpResponseSnapshot
        {
            Status = response.Status,
            Headers = headers,
            Body = response.Body,
        };
    }
}
