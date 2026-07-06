using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mockifyr.Differential.Generator;

/// <summary>
/// A random-helper case (G2e). The helpers' output is non-deterministic, so a byte diff is
/// impossible; instead every case carries a <see cref="IsValid"/> structural contract (charset +
/// length, set membership, or numeric range). The differential test asserts the <em>oracle</em>
/// output satisfies the contract (proving it is real WireMock behavior, not a self-assertion) and
/// then requires Mockifyr's output to satisfy the very same contract.
/// </summary>
public sealed record RandomCase(string Description, string WireMockJson, RequestSpec Request, Func<string, bool> IsValid);

/// <summary>
/// Structural-parity cases for the WireMock helpers whose output can't be byte-diffed: the random
/// helpers (<c>randomValue</c> UUID + character types, <c>pickRandom</c>, <c>randomInt</c>, bounded
/// <c>randomDecimal</c>) and the host-specific <c>hostname</c> (G2h). See docs/parity/g2-response.md.
/// </summary>
public static class RandomScenarios
{
    private static readonly Regex Uuid = new(
        "^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", RegexOptions.Compiled);

    public static IEnumerable<RandomCase> All()
    {
        yield return Case("uuid", "{{randomValue type='UUID'}}", Uuid.IsMatch);
        yield return Case("alphanumeric", "{{randomValue length=12 type='ALPHANUMERIC'}}", Matches("^[a-z0-9]{12}$"));
        yield return Case("alphabetic", "{{randomValue length=8 type='ALPHABETIC'}}", Matches("^[a-z]{8}$"));
        yield return Case("numeric", "{{randomValue length=6 type='NUMERIC'}}", Matches("^[0-9]{6}$"));
        yield return Case("hexadecimal", "{{randomValue length=16 type='HEXADECIMAL'}}", Matches("^[0-9a-f]{16}$"));
        yield return Case(
            "alphanumeric-upper", "{{randomValue length=10 type='ALPHANUMERIC' uppercase=true}}",
            Matches("^[A-Z0-9]{10}$"));

        // ALPHANUMERIC_AND_SYMBOLS: lowercase + digits + the printable-ASCII symbol set the oracle uses
        // (no upper-case letters, no space, no `~`). Charset membership rather than a regex — the set
        // contains regex metacharacters. See docs/parity/g2-response.md.
        const string symbols = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}";
        const string alphanumericAndSymbols = "abcdefghijklmnopqrstuvwxyz0123456789" + symbols;
        yield return Case(
            "alphanumeric-and-symbols", "{{randomValue length=40 type='ALPHANUMERIC_AND_SYMBOLS'}}",
            body => body.Length == 40 && body.All(alphanumericAndSymbols.Contains));

        yield return Case("pick-multi", "{{pickRandom 'a' 'b' 'c'}}", body => body is "a" or "b" or "c");
        yield return Case("pick-single", "{{pickRandom 'solo'}}", body => body == "solo");

        yield return Case("randomInt-bounded", "{{randomInt lower=100 upper=110}}",
            body => int.TryParse(body, out var v) && v is >= 100 and < 110);
        yield return Case("randomInt-unbounded", "{{randomInt}}", body => int.TryParse(body, out _));

        yield return Case("randomDecimal-bounded", "{{randomDecimal lower=1.5 upper=2.5}}",
            body => double.TryParse(body, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
                    && v is >= 1.5 and <= 2.5);

        // G2h hostname is host-specific (the oracle sees its container's name, Mockifyr the test
        // host's) — not byte-diffable, but both must resolve to a non-empty hostname-shaped string.
        yield return Case("hostname", "{{hostname}}", Matches("^[A-Za-z0-9._-]+$"));
    }

    private static Func<string, bool> Matches(string pattern)
    {
        var regex = new Regex(pattern, RegexOptions.Compiled);
        return regex.IsMatch;
    }

    private static RandomCase Case(string description, string body, Func<string, bool> isValid)
    {
        var url = "/rnd/" + description;
        var mapping = new Dictionary<string, object>
        {
            ["request"] = new Dictionary<string, object> { ["method"] = "GET", ["urlPath"] = url },
            ["response"] = new Dictionary<string, object>
            {
                ["status"] = 200,
                ["transformers"] = new object[] { "response-template" },
                ["body"] = body,
            },
        };

        var request = new RequestSpec { Method = "GET", Url = url };
        return new RandomCase($"random[{description}]", JsonSerializer.Serialize(mapping), request, isValid);
    }
}
