namespace Mockifyr.Outbound;

/// <summary>
/// The state of a live recording (G12d): while active, the mock-serving facade proxies requests to
/// <see cref="TargetBaseUrl"/> and records the generated stubs here. Shared as a singleton between the
/// admin endpoints (start/stop/snapshot) and the mock-serving fallback. Thread-safe.
/// </summary>
public sealed class RecordingSession
{
    private readonly Lock _gate = new();
    private readonly List<string> _stubs = [];
    private string? _target;

    /// <summary>The upstream base URL to proxy to while recording, or null when not recording.</summary>
    public string? TargetBaseUrl
    {
        get
        {
            lock (_gate)
            {
                return _target;
            }
        }
    }

    /// <summary>Begins recording against a target, discarding any prior capture.</summary>
    public void Start(string targetBaseUrl)
    {
        lock (_gate)
        {
            _target = targetBaseUrl;
            _stubs.Clear();
        }
    }

    /// <summary>Records a generated stub captured during the session.</summary>
    public void Record(string stubJson)
    {
        lock (_gate)
        {
            _stubs.Add(stubJson);
        }
    }

    /// <summary>Returns the captured stubs so far without stopping.</summary>
    public IReadOnlyList<string> Snapshot()
    {
        lock (_gate)
        {
            return [.. _stubs];
        }
    }

    /// <summary>Ends recording and returns the captured stubs.</summary>
    public IReadOnlyList<string> Stop()
    {
        lock (_gate)
        {
            var captured = _stubs.ToArray();
            _target = null;
            _stubs.Clear();
            return captured;
        }
    }
}
