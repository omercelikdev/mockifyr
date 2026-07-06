using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of the <c>jwks</c> helper (G15h): the JSON Web Key Set for the RS256 public key that the
/// <c>jwt</c> helper signs with, served from a stub (`{{jwks}}`). The key is random per instance, so — like
/// the token (G15b) — it can't be byte-diffed. It is validated <b>structurally</b> (the racy-feature
/// method): the same jwks stub is served by the oracle (WireMock + the JWT extension) and Mockifyr, and
/// each side's output must satisfy the JWKS shape contract. Plus a <b>self-consistency</b> anchor: on each
/// side an RS256 token minted by <c>{{jwt}}</c> must verify against the public key in that side's
/// <c>{{jwks}}</c> — proving the jwks exposes the same key that signs the tokens, exactly as the reference
/// does. The oracle satisfying both is what makes them real WireMock behavior. Requires Docker.
/// </summary>
public sealed class G15hJwksTests : IAsyncLifetime
{
    private const string JwtStub =
        "{\"request\":{\"method\":\"GET\",\"url\":\"/jwt\"}," +
        "\"response\":{\"status\":200,\"transformers\":[\"response-template\"],\"body\":\"{{jwt alg='RS256' sub='u1'}}\"}}";

    private const string JwksStub =
        "{\"request\":{\"method\":\"GET\",\"url\":\"/jwks\"}," +
        "\"response\":{\"status\":200,\"transformers\":[\"response-template\"],\"body\":\"{{jwks}}\"}}";

    private readonly WireMockJwtOracle _oracle = new();
    private readonly WebApplicationFactory<Program> _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    [Fact]
    public async Task Jwks_StructureAndSelfConsistency_MatchTheOracle()
    {
        using var mockifyrClient = _mockifyr.CreateClient();

        await AssertSideAsync(_oracle.Client, "oracle");
        await AssertSideAsync(mockifyrClient, "mockifyr");
    }

    // Load both stubs on a side, then assert its jwks shape and that its RS256 token verifies against it.
    private static async Task AssertSideAsync(HttpClient client, string side)
    {
        await LoadAsync(client, JwtStub);
        await LoadAsync(client, JwksStub);

        var token = await client.GetStringAsync("/jwt");
        var jwks = await client.GetStringAsync("/jwks");

        var key = SingleKey(jwks, side);
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.False(string.IsNullOrEmpty(key.GetProperty("kid").GetString()), $"{side}: kid must be present");

        var n = Base64UrlDecode(key.GetProperty("n").GetString()!);
        var e = Base64UrlDecode(key.GetProperty("e").GetString()!);
        Assert.True(n.Length >= 256, $"{side}: modulus too short ({n.Length} bytes) for RS256");
        Assert.True(e.Length is > 0 and <= 8, $"{side}: exponent length {e.Length} out of range");

        // Self-consistency: the token's RS256 signature must verify against this jwks public key.
        Assert.True(VerifyRs256(token, n, e), $"{side}: the RS256 token does not verify against its own jwks key");
        // And the token's header kid must name the jwks key, so a verifier can select it.
        Assert.Equal(key.GetProperty("kid").GetString(), TokenHeaderKid(token));
    }

    private static JsonElement SingleKey(string jwks, string side)
    {
        using var document = JsonDocument.Parse(jwks);
        var keys = document.RootElement.GetProperty("keys");
        Assert.True(keys.GetArrayLength() == 1, $"{side}: expected exactly one key, got {keys.GetArrayLength()}");
        return keys[0].Clone();
    }

    private static bool VerifyRs256(string token, byte[] modulus, byte[] exponent)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
        var signingInput = Encoding.UTF8.GetBytes(parts[0] + "." + parts[1]);
        var signature = Base64UrlDecode(parts[2]);
        return rsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    private static string? TokenHeaderKid(string token)
    {
        using var document = JsonDocument.Parse(Base64UrlDecode(token.Split('.')[0]));
        return document.RootElement.TryGetProperty("kid", out var kid) ? kid.GetString() : null;
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch { 2 => padded + "==", 3 => padded + "=", _ => padded };
        return Convert.FromBase64String(padded);
    }

    private static async Task LoadAsync(HttpClient client, string mapping)
    {
        using var content = new StringContent(mapping, Encoding.UTF8, "application/json");
        (await client.PostAsync("/__admin/mappings", content)).EnsureSuccessStatusCode();
    }
}
