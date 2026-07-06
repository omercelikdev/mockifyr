using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using HandlebarsDotNet;

namespace Mockifyr.Templating;

/// <summary>
/// The <c>jwt</c> templating helper (G15): <c>{{jwt sub='u1' role='admin'}}</c> renders a signed JWT,
/// mirroring WireMock's JWT extension. Claim defaults match the reference — <c>iss=wiremock</c>,
/// <c>aud=wiremock.io</c>, <c>sub=user-123</c>, <c>iat=now</c>, <c>exp=now+maxAge</c> (default
/// <c>36500 days</c>) — and any non-reserved parameter becomes a private claim. Signed with HS256.
///
/// <para>WireMock's default signing secret is random per instance and <c>iat</c> is the current time,
/// so the token can't be byte-diffed; it is validated by <b>content parity</b> — the decoded header and
/// claims match the reference — plus a structural check on the racy <c>iat</c>/<c>exp</c> and the
/// signature. RS256, configurable secrets, <c>nbf</c>, and array claims are deferred.</para>
/// </summary>
internal static class JwtHelpers
{
    // WireMock mints a random 36-char secret per start; Mockifyr uses a fixed default (configurable is
    // a follow-up). Content parity does not depend on the secret, only the claims.
    private const string DefaultSecret = "mockifyr-default-hs256-secret";

    // The RSA key + key id for RS256, generated once per instance (like the reference extension). Both
    // are effectively random, so — like the HS256 signature — they are excluded from content parity.
    private static readonly RSA RsaKey = RSA.Create(2048);
    private static readonly string Kid = Base64Url(RandomNumberGenerator.GetBytes(23))[..30];

    // Handled specially or consumed (not emitted as private claims). WireMock reserves iss/aud/sub/
    // exp/nbf; Mockifyr also consumes maxAge. `alg` selects the algorithm AND (like the reference) leaks
    // into the payload as a claim, so it is not reserved.
    private static readonly HashSet<string> Reserved =
        new(StringComparer.Ordinal) { "exp", "iss", "aud", "sub", "nbf", "maxAge" };

    public static void Register(IHandlebars handlebars) =>
        handlebars.RegisterHelper("jwt", (_, arguments) => CreateToken(arguments.Hash));

    private static object CreateToken(IReadOnlyDictionary<string, object>? hash)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = new JsonObject
        {
            ["exp"] = now.Add(ParseMaxAge(Get(hash, "maxAge"))).ToUnixTimeSeconds(),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["iss"] = Get(hash, "iss") ?? "wiremock",
            ["aud"] = Get(hash, "aud") ?? "wiremock.io",
            ["sub"] = Get(hash, "sub") ?? "user-123",
        };

        if (hash is not null)
        {
            foreach (var (key, value) in hash)
            {
                if (!Reserved.Contains(key))
                {
                    payload[key] = ToNode(value);
                }
            }
        }

        var alg = Get(hash, "alg") ?? "HS256";
        var header = new JsonObject { ["alg"] = alg, ["typ"] = "JWT" };
        if (alg == "RS256")
        {
            header["kid"] = Kid;
        }

        var signingInput = Base64Url(Bytes(header)) + "." + Base64Url(Bytes(payload));
        var signature = alg == "RS256"
            ? RsaKey.SignData(Encoding.UTF8.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            : Sign(signingInput);
        return signingInput + "." + Base64Url(signature);
    }

    // "amount unit" (e.g. "12 days"); default 36500 days, matching the reference.
    private static TimeSpan ParseMaxAge(string? maxAge)
    {
        var parts = maxAge?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not { Length: 2 } || !long.TryParse(parts[0], out var amount))
        {
            return TimeSpan.FromDays(36500);
        }

        return parts[1].ToLowerInvariant() switch
        {
            "seconds" => TimeSpan.FromSeconds(amount),
            "minutes" => TimeSpan.FromMinutes(amount),
            "hours" => TimeSpan.FromHours(amount),
            "days" => TimeSpan.FromDays(amount),
            _ => TimeSpan.FromDays(36500),
        };
    }

    private static JsonNode? ToNode(object? value) => value switch
    {
        null => null,
        bool b => b,
        int i => i,
        long l => l,
        double d => d,
        _ => value.ToString(),
    };

    private static byte[] Sign(string signingInput)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(DefaultSecret));
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
    }

    private static byte[] Bytes(JsonNode node) => Encoding.UTF8.GetBytes(node.ToJsonString());

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string? Get(IReadOnlyDictionary<string, object>? hash, string key) =>
        hash is not null && hash.TryGetValue(key, out var value) ? value?.ToString() : null;
}
