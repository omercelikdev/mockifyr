using System.Globalization;
using System.Text;
using HandlebarsDotNet;

namespace Mockifyr.Templating;

/// <summary>
/// WireMock's random-value Handlebars helpers (G2e): <c>randomValue</c>, <c>pickRandom</c>,
/// <c>randomInt</c>, and <c>randomDecimal</c>. Their output is non-deterministic, so parity is
/// validated <em>structurally</em> against the oracle (charset + length, set membership, numeric
/// range) rather than by a byte diff — see docs/parity/g2-response.md. The character alphabets and
/// the half-open <c>[lower, upper)</c> integer range were pinned by sampling the oracle.
/// </summary>
internal static class RandomHelpers
{
    private const string Lowercase = "abcdefghijklmnopqrstuvwxyz";
    private const string Digits = "0123456789";
    private const string HexDigits = "0123456789abcdef";

    public static void Register(IHandlebars handlebars)
    {
        handlebars.RegisterHelper("randomValue", (_, arguments) => RandomValue(arguments));
        handlebars.RegisterHelper("pickRandom", (_, arguments) => PickRandom(arguments));
        handlebars.RegisterHelper("randomInt", (_, arguments) => RandomInt(arguments));
        handlebars.RegisterHelper("randomDecimal", (_, arguments) => RandomDecimal(arguments));
    }

    // --- randomValue --------------------------------------------------------------------------

    private static object RandomValue(Arguments arguments)
    {
        var type = (Hash(arguments, "type") ?? "ALPHANUMERIC").ToUpperInvariant();
        if (type == "UUID")
        {
            return Guid.NewGuid().ToString();
        }

        var length = HashInt(arguments, "length") ?? 36;
        var uppercase = string.Equals(Hash(arguments, "uppercase"), "true", StringComparison.OrdinalIgnoreCase);

        var alphabet = type switch
        {
            "ALPHABETIC" => Lowercase,
            "NUMERIC" => Digits,
            "HEXADECIMAL" => HexDigits,
            _ => Lowercase + Digits, // ALPHANUMERIC (the default)
        };

        if (uppercase)
        {
            alphabet = alphabet.ToUpperInvariant();
        }

        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(alphabet[Random.Shared.Next(alphabet.Length)]);
        }

        return builder.ToString();
    }

    // --- pickRandom ---------------------------------------------------------------------------

    private static object PickRandom(Arguments arguments)
    {
        // Positional values only; a trailing hash dictionary (if any) is not a candidate.
        var candidates = new List<object?>();
        for (var i = 0; i < arguments.Length; i++)
        {
            if (arguments[i] is not IDictionary<string, object> and { } value)
            {
                candidates.Add(value);
            }
        }

        return candidates.Count == 0
            ? string.Empty
            : candidates[Random.Shared.Next(candidates.Count)]?.ToString() ?? string.Empty;
    }

    // --- randomInt ----------------------------------------------------------------------------

    private static object RandomInt(Arguments arguments)
    {
        var lower = HashInt(arguments, "lower");
        var upper = HashInt(arguments, "upper");

        // The oracle's range is half-open [lower, upper), which matches Random.Next(lower, upper).
        return lower is { } lo && upper is { } hi && lo < hi
            ? Random.Shared.Next(lo, hi)
            : Random.Shared.Next(int.MinValue, int.MaxValue);
    }

    // --- randomDecimal ------------------------------------------------------------------------

    private static object RandomDecimal(Arguments arguments)
    {
        var lower = HashDouble(arguments, "lower");
        var upper = HashDouble(arguments, "upper");

        var value = lower is { } lo && upper is { } hi && lo < hi
            ? lo + (Random.Shared.NextDouble() * (hi - lo))
            : Random.Shared.NextDouble() * double.MaxValue;

        // Emit with the invariant culture so the decimal point is stable regardless of locale.
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    // --- shared -------------------------------------------------------------------------------

    private static string? Hash(Arguments arguments, string key) =>
        arguments.Hash is { } hash && hash.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static int? HashInt(Arguments arguments, string key) =>
        int.TryParse(Hash(arguments, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static double? HashDouble(Arguments arguments, string key) =>
        double.TryParse(Hash(arguments, key), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
