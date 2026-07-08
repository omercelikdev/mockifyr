using Mockifyr.Adapters.MappingJson;
using Mockifyr.Core;

namespace Mockifyr.Outbound;

/// <summary>A recorded exchange: the generated stub JSON plus the response that was captured.</summary>
public sealed record RecordedExchange(string StubJson, CanonicalResponse CapturedResponse);

/// <summary>
/// Record mode (G9, verified by the differential suite): proxy a request to the target upstream,
/// capture the response, and generate a stub that replays it. Reuses <see cref="ProxyResponder"/> for the outbound
/// call (I/O at the facade edge) and <see cref="RecordingJsonWriter"/> for the stub JSON. Filters,
/// body-file extraction, and repeat-request → scenario generation are deferred.
/// </summary>
public sealed class StubRecorder(HttpClient? client = null)
{
    private readonly ProxyResponder _proxy = new(client);

    public async Task<RecordedExchange> RecordAsync(
        string targetBaseUrl, CanonicalRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _proxy.ProxyAsync(new ProxyDirective(targetBaseUrl), request, cancellationToken)
            .ConfigureAwait(false);

        return new RecordedExchange(RecordingJsonWriter.ToStubJson(request, response), response);
    }
}
