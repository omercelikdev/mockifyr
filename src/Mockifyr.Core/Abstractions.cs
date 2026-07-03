namespace Mockifyr.Core;

/// <summary>
/// Tenant-scoped store of stub definitions. This is the persistence seam: the default
/// implementation is in-memory; durable providers slot in behind this contract without the
/// engine changing. There is deliberately no tenant-less overload — forgetting the scope must
/// be a compile error. See docs/decisions/0003 and 0006.
/// </summary>
public interface IStubStore
{
    /// <summary>Returns the stubs owned by <paramref name="tenant"/>.</summary>
    IReadOnlyList<StubMapping> GetStubs(TenantId tenant);

    /// <summary>Adds or replaces a stub.</summary>
    void Put(StubMapping stub);

    /// <summary>Removes a stub from a tenant.</summary>
    void Remove(TenantId tenant, Guid id);
}

/// <summary>
/// An atomic, composable matcher. Logical operators (and/or/not) implement this interface too,
/// so composition comes for free. This is also the custom-matcher extension seam (G10).
/// </summary>
public interface IMatcher
{
    /// <summary>Evaluates the input and returns an exact match or a distance.</summary>
    MatchResult Match(MatchInput input);
}

/// <summary>Renders a <see cref="ResponseDefinition"/> into a concrete response (templating).</summary>
public interface IResponseRenderer
{
    /// <summary>Renders the definition against the given context.</summary>
    CanonicalResponse Render(ResponseDefinition definition, RenderContext context);
}

/// <summary>
/// Tenant-scoped scenario state store. Read before matching (eligibility) and written after
/// matching (transition).
/// </summary>
public interface IScenarioStateStore
{
    /// <summary>Gets the current state of a scenario (default <c>Started</c>).</summary>
    string GetState(TenantId tenant, string scenario);

    /// <summary>Sets the state of a scenario.</summary>
    void SetState(TenantId tenant, string scenario, string state);
}

/// <summary>Tenant-scoped request journal, used by verification and near-miss diagnostics.</summary>
public interface IRequestJournal
{
    /// <summary>Records a served event.</summary>
    void Record(ServeEvent serveEvent);

    /// <summary>Queries recorded events for a tenant.</summary>
    IReadOnlyList<ServeEvent> Query(TenantId tenant, ServeEventQuery query);
}

/// <summary>
/// A serve-event listener, fired asynchronously after a request is served. Webhooks/callbacks
/// are an implementation of this seam; their outbound I/O lives outside the pure engine core.
/// </summary>
public interface IServeEventListener
{
    /// <summary>Called after a request is served.</summary>
    Task OnServeEventAsync(ServeEvent serveEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Renders a template string against a serve event's original (triggering) request — the model
/// WireMock exposes to webhooks as <c>originalRequest</c> (G3b). The implementation lives in the
/// templating edge; the webhook listener depends only on this contract, keeping outbound I/O and
/// templating decoupled.
/// </summary>
public interface IServeEventTemplateRenderer
{
    /// <summary>Renders <paramref name="template"/> with the original request exposed as <c>originalRequest</c>.</summary>
    string Render(string template, CanonicalRequest originalRequest);
}

/// <summary>
/// Signals that the underlying store changed out-of-band (e.g. an edited file or a DB row) so
/// the engine can reload its in-memory compiled index. This — not reading the DB on the hot
/// path — is how "edit directly and have it reflected" is served. See docs/decisions/0006.
/// </summary>
public interface IStubChangeSource
{
    /// <summary>Raised when the external source has changed and a reload is warranted.</summary>
    event EventHandler<TenantId> Changed;
}
