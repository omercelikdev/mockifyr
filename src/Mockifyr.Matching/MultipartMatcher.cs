using System.Text;
using Mockifyr.Core;

namespace Mockifyr.Matching;

/// <summary>How many multipart parts must satisfy a <see cref="MultipartMatcher"/>.</summary>
public enum MultipartMatchingType
{
    /// <summary>At least one part must satisfy the body patterns (the default).</summary>
    Any,

    /// <summary>Every part must satisfy the body patterns.</summary>
    All,
}

/// <summary>
/// Matches a <c>multipart/form-data</c> request via the <c>multipartPatterns</c> construct. A part
/// satisfies the pattern when <b>all</b> of its body patterns match that part's body;
/// <c>matchingType</c> then decides whether <b>any</b> (default) or <b>all</b> parts must satisfy.
/// A non-multipart request (no parsed parts) never matches. Verified by the differential suite: the
/// per-pattern <c>name</c> is a no-op, so it is intentionally ignored — see
/// docs/parity/g1-matching.md.
/// </summary>
public sealed class MultipartMatcher(IReadOnlyList<IValueMatcher> bodyPatterns, MultipartMatchingType matchingType)
    : IMatcher
{
    /// <inheritdoc />
    public MatchResult Match(MatchInput input)
    {
        var parts = input.Request.Parts;
        if (parts.Count == 0)
        {
            return MatchResult.NoMatch(1d);
        }

        var matched = matchingType == MultipartMatchingType.All
            ? parts.All(Satisfies)
            : parts.Any(Satisfies);

        return matched ? MatchResult.Exact : MatchResult.NoMatch(1d);
    }

    private bool Satisfies(MultipartPart part)
    {
        var text = Encoding.UTF8.GetString(part.Body);
        return bodyPatterns.All(pattern => pattern.Match(part.Body.Length > 0, [text]).IsExactMatch);
    }
}
