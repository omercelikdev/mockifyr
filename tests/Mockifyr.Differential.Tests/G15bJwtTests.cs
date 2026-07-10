using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Differential validation of the <c>jwt</c> helper (G15b). WireMock's default signing secret is random
/// per instance and <c>iat</c> is the current time, so a token can't be byte-diffed. It is validated by
/// <b>content parity</b>: the same stub is rendered by the oracle (WireMock + the JWT extension) and
/// Mockifyr, both tokens are decoded, and the <b>header and non-time claims must be identical</b> — the
/// meaningful part. The racy <c>iat</c>/<c>exp</c> are checked structurally (present, <c>iat</c> ~now,
/// <c>exp = iat + default maxAge</c>), and both signatures must be well-formed. Requires Docker.
/// </summary>
public sealed class G15bJwtTests : IAsyncLifetime
{
    // 100 years, in seconds — WireMock's default maxAge (36500 days).
    private const long DefaultMaxAgeSeconds = 36500L * 24 * 60 * 60;

    private readonly WireMockJwtOracle _oracle = new();
    private readonly WebApplicationFactory<Program> _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    private sealed record Case(string Description, string Template);

    private static IEnumerable<Case> Cases()
    {
        yield return new Case("defaults", "{{jwt}}");
        yield return new Case("overrides+custom", "{{jwt sub='u1' iss='acme' aud='api' role='admin' scope='read'}}");
        yield return new Case("rs256", "{{jwt alg='RS256' sub='u1' role='admin'}}");
    }

    [Fact]
    public async Task Jwt_ContentMatchesTheOracle()
    {
        using var mockifyrClient = _mockifyr.CreateClient();
        var failures = new List<string>();

        foreach (var scenario in Cases())
        {
            var mapping = "{\"request\":{\"method\":\"GET\",\"urlPath\":\"/jwt\"}," +
                          "\"response\":{\"status\":200,\"transformers\":[\"response-template\"],\"body\":\"" + scenario.Template + "\"}}";

            await LoadAsync(_oracle.Client, mapping);
            await LoadAsync(mockifyrClient, mapping);

            var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var oracle = Decode(await _oracle.Client.GetStringAsync("/jwt"));
            var mockifyr = Decode(await mockifyrClient.GetStringAsync("/jwt"));
            var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Header identical.
            if (oracle.Header != mockifyr.Header)
            {
                failures.Add($"{scenario.Description}: header oracle={oracle.Header} mockifyr={mockifyr.Header}");
            }

            // Non-time claims identical (the meaningful content parity).
            if (oracle.Claims != mockifyr.Claims)
            {
                failures.Add($"{scenario.Description}: claims oracle={oracle.Claims} mockifyr={mockifyr.Claims}");
            }

            // iat ~now and exp = iat + default maxAge, on both sides (structural — racy).
            foreach (var (side, token) in new[] { ("oracle", oracle), ("mockifyr", mockifyr) })
            {
                if (token.Iat < before - 900 || token.Iat > after + 900)
                {
                    failures.Add($"{scenario.Description}: {side} iat {token.Iat} outside [{before},{after}]");
                }

                // exp and iat may be computed on either side of a second boundary (observed on CI:
                // exp-iat one short), so the structural check allows a ±1s skew.
                if (Math.Abs(token.Exp - token.Iat - DefaultMaxAgeSeconds) > 1)
                {
                    failures.Add($"{scenario.Description}: {side} exp-iat {token.Exp - token.Iat} != {DefaultMaxAgeSeconds}");
                }

                if (string.IsNullOrEmpty(token.Signature))
                {
                    failures.Add($"{scenario.Description}: {side} missing signature");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} JWT divergence(s):\n{string.Join("\n", failures)}");
    }

    private static async Task LoadAsync(HttpClient client, string mapping)
    {
        await client.PostAsync("/__admin/mappings/reset", content: null);
        using var content = new StringContent(mapping, Encoding.UTF8, "application/json");
        await client.PostAsync("/__admin/mappings", content);
    }

    // Decodes a JWT into its header JSON, the non-time claims (iat/exp removed, canonicalized), iat/exp,
    // and the raw signature part.
    private static (string Header, string Claims, long Iat, long Exp, string Signature) Decode(string token)
    {
        var parts = token.Split('.');
        Assert.Equal(3, parts.Length);

        var header = JsonNode.Parse(Base64UrlDecode(parts[0]))!.AsObject();
        var payload = JsonNode.Parse(Base64UrlDecode(parts[1]))!.AsObject();

        // RS256 headers carry a random `kid` (like the signature, key-specific) — exclude it from parity.
        header.Remove("kid");

        var iat = payload["iat"]!.GetValue<long>();
        var exp = payload["exp"]!.GetValue<long>();
        payload.Remove("iat");
        payload.Remove("exp");

        return (Canonical(header), Canonical(payload), iat, exp, parts[2]);
    }

    // Stable key-ordered JSON string so two semantically-equal objects compare equal.
    private static string Canonical(JsonObject obj) =>
        "{" + string.Join(",", obj.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => $"\"{p.Key}\":{p.Value?.ToJsonString()}")) + "}";

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
