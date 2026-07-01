namespace Mockifyr.Differential.Harness;

/// <summary>
/// Compares two response snapshots after canonicalization and masking. Status and body are
/// compared strictly. Only the headers explicitly declared by the stub are compared; transport-
/// and server-injected headers (e.g. <c>Matched-Stub-Id</c>, <c>Transfer-Encoding</c>) are
/// masked on both sides. The masked set widens as the response roadmap (G2/G12) progresses.
/// </summary>
public static class ResponseDiffer
{
    /// <summary>Diffs the oracle response against Mockifyr's, comparing the given declared headers.</summary>
    public static DiffResult Compare(
        HttpResponseSnapshot oracle,
        HttpResponseSnapshot mockifyr,
        IEnumerable<string> declaredHeaders)
    {
        var differences = new List<string>();

        if (oracle.Status != mockifyr.Status)
        {
            differences.Add($"status: oracle={oracle.Status} mockifyr={mockifyr.Status}");
        }

        if (!oracle.Body.AsSpan().SequenceEqual(mockifyr.Body))
        {
            differences.Add($"body: oracle=\"{oracle.BodyAsText}\" mockifyr=\"{mockifyr.BodyAsText}\"");
        }

        foreach (var name in declaredHeaders)
        {
            var oracleValue = oracle.Headers.TryGetValue(name, out var ov) ? string.Join(",", ov) : null;
            var mockifyrValue = mockifyr.Headers.TryGetValue(name, out var mv) ? string.Join(",", mv) : null;

            if (!string.Equals(oracleValue, mockifyrValue, StringComparison.Ordinal))
            {
                differences.Add(
                    $"header[{name}]: oracle={oracleValue ?? "<absent>"} mockifyr={mockifyrValue ?? "<absent>"}");
            }
        }

        return new DiffResult(differences);
    }
}
