using System.Text;

namespace Mockifyr.Differential.Harness;

/// <summary>A comparable snapshot of a response from either side.</summary>
public sealed record HttpResponseSnapshot
{
    /// <summary>Status code.</summary>
    public required int Status { get; init; }

    /// <summary>Response headers (multi-valued, case-insensitive keys).</summary>
    public required IReadOnlyDictionary<string, string[]> Headers { get; init; }

    /// <summary>Response body.</summary>
    public required byte[] Body { get; init; }

    /// <summary>The body decoded as UTF-8, for readable diffs.</summary>
    public string BodyAsText => Encoding.UTF8.GetString(Body);
}

/// <summary>The result of diffing two response snapshots.</summary>
public sealed record DiffResult(IReadOnlyList<string> Differences)
{
    /// <summary>True when the two responses are equivalent (after canonicalization/masking).</summary>
    public bool IsMatch => Differences.Count == 0;

    /// <summary>A human-readable report of the differences.</summary>
    public string Report => IsMatch ? "<no differences>" : string.Join(Environment.NewLine, Differences);
}
