using System.Text.Json;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>
/// Matches a JSON value semantically. Key order and whitespace are irrelevant; numbers compare
/// by value (<c>1</c> == <c>1.0</c>); types are significant (<c>1</c> != <c>"1"</c>). The
/// <c>ignoreArrayOrder</c> option relaxes array comparison to multiset equality, and
/// <c>ignoreExtraElements</c> allows the actual document to carry extra object properties or
/// trailing array items; their exact semantics are verified by the differential suite. See
/// docs/parity/g1-matching.md.
/// </summary>
public sealed class EqualToJsonValueMatcher : IValueMatcher
{
    private readonly JsonElement _expected;
    private readonly bool _valid;
    private readonly bool _ignoreArrayOrder;
    private readonly bool _ignoreExtraElements;

    /// <summary>Creates the matcher from the expected JSON text and the two ignore options.</summary>
    public EqualToJsonValueMatcher(string expectedJson, bool ignoreArrayOrder, bool ignoreExtraElements)
    {
        _ignoreArrayOrder = ignoreArrayOrder;
        _ignoreExtraElements = ignoreExtraElements;
        try
        {
            using var doc = JsonDocument.Parse(expectedJson);
            _expected = doc.RootElement.Clone();
            _valid = true;
        }
        catch (JsonException)
        {
            _valid = false;
        }
    }

    /// <inheritdoc />
    public MatchResult Match(bool present, IReadOnlyList<string> values)
    {
        if (!_valid || !present || values.Count == 0)
        {
            return MatchResult.NoMatch(1d);
        }

        JsonElement actual;
        try
        {
            using var doc = JsonDocument.Parse(values[0]);
            actual = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return MatchResult.NoMatch(1d);
        }

        return DeepEquals(_expected, actual, _ignoreArrayOrder, _ignoreExtraElements)
            ? MatchResult.Exact
            : MatchResult.NoMatch(1d);
    }

    private static bool DeepEquals(JsonElement expected, JsonElement actual, bool ignoreArrayOrder, bool ignoreExtra)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            return false;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                return ObjectEquals(expected, actual, ignoreArrayOrder, ignoreExtra);
            case JsonValueKind.Array:
                return ArrayEquals(expected, actual, ignoreArrayOrder, ignoreExtra);
            case JsonValueKind.String:
                return string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal);
            case JsonValueKind.Number:
                return NumbersEqual(expected, actual);
            default:
                // True, False, Null: value kinds already matched.
                return true;
        }
    }

    private static bool ObjectEquals(JsonElement expected, JsonElement actual, bool ignoreArrayOrder, bool ignoreExtra)
    {
        var expectedCount = 0;
        foreach (var property in expected.EnumerateObject())
        {
            expectedCount++;
            if (!actual.TryGetProperty(property.Name, out var actualValue) ||
                !DeepEquals(property.Value, actualValue, ignoreArrayOrder, ignoreExtra))
            {
                return false;
            }
        }

        if (!ignoreExtra)
        {
            var actualCount = 0;
            foreach (var _ in actual.EnumerateObject())
            {
                actualCount++;
            }

            return actualCount == expectedCount;
        }

        return true;
    }

    private static bool ArrayEquals(JsonElement expected, JsonElement actual, bool ignoreArrayOrder, bool ignoreExtra)
    {
        var expectedItems = expected.EnumerateArray().ToList();
        var actualItems = actual.EnumerateArray().ToList();

        if (ignoreArrayOrder)
        {
            var consumed = new bool[actualItems.Count];
            foreach (var expectedItem in expectedItems)
            {
                var matchedIndex = -1;
                for (var i = 0; i < actualItems.Count; i++)
                {
                    if (!consumed[i] && DeepEquals(expectedItem, actualItems[i], ignoreArrayOrder, ignoreExtra))
                    {
                        matchedIndex = i;
                        break;
                    }
                }

                if (matchedIndex < 0)
                {
                    return false;
                }

                consumed[matchedIndex] = true;
            }

            // Without ignoreExtra, every actual element must be consumed too (multiset equality).
            return ignoreExtra || consumed.All(c => c);
        }

        // Ordered comparison. Under ignoreExtraElements, extra trailing array items are allowed
        // (verified by the differential suite), so the expected array must match a prefix.
        if (ignoreExtra)
        {
            if (actualItems.Count < expectedItems.Count)
            {
                return false;
            }
        }
        else if (actualItems.Count != expectedItems.Count)
        {
            return false;
        }

        for (var i = 0; i < expectedItems.Count; i++)
        {
            if (!DeepEquals(expectedItems[i], actualItems[i], ignoreArrayOrder, ignoreExtra))
            {
                return false;
            }
        }

        return true;
    }

    private static bool NumbersEqual(JsonElement expected, JsonElement actual)
    {
        if (expected.TryGetDecimal(out var expectedDecimal) && actual.TryGetDecimal(out var actualDecimal))
        {
            return expectedDecimal == actualDecimal;
        }

        return expected.GetDouble().Equals(actual.GetDouble());
    }
}
