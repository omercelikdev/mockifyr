namespace Mockifyr.Differential.Generator;

/// <summary>A request replayed identically against both the oracle and Mockifyr.</summary>
public sealed record RequestSpec
{
    /// <summary>HTTP method.</summary>
    public required string Method { get; init; }

    /// <summary>URL: path plus optional query string.</summary>
    public required string Url { get; init; }

    /// <summary>Optional request headers.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Headers { get; init; } = [];

    /// <summary>Optional request body.</summary>
    public byte[]? Body { get; init; }
}
