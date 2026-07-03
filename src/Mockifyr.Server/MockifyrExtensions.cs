using Mockifyr.Core;

namespace Mockifyr.Server;

/// <summary>
/// The public extension builder (G10). Collects user-supplied extensions — custom request matchers,
/// serve-event listeners, template helpers, and response transformers — which
/// <c>AddMockifyr(cfg =&gt; …)</c> wires into the engine, renderer, and adapter. WireMock's other
/// extension seams (<see cref="IResponseDefinitionTransformer"/>, <see cref="ITemplateModelProvider"/>,
/// <see cref="IAdminApiExtension"/>, <see cref="IMappingsLoader"/>) are public on
/// <c>Mockifyr.Core</c> and wired incrementally.
/// </summary>
public sealed class MockifyrExtensions
{
    internal List<IServeEventListener> ServeEventListeners { get; } = [];

    internal List<IResponseTransformer> ResponseTransformers { get; } = [];

    internal List<TemplateHelperExtension> TemplateHelpers { get; } = [];

    internal List<(string Name, IMatcher Matcher)> Matchers { get; } = [];

    /// <summary>Registers a named custom matcher, referenced from stub JSON via <c>customMatcher</c>.</summary>
    public MockifyrExtensions AddMatcher(string name, IMatcher matcher)
    {
        Matchers.Add((name, matcher));
        return this;
    }

    /// <summary>Registers a serve-event listener, fired after every request is served.</summary>
    public MockifyrExtensions AddServeEventListener(IServeEventListener listener)
    {
        ServeEventListeners.Add(listener);
        return this;
    }

    /// <summary>Registers a template helper usable as <c>{{name …}}</c> in response/webhook templates.</summary>
    public MockifyrExtensions AddTemplateHelper(string name, Func<IReadOnlyList<object?>, string> render)
    {
        TemplateHelpers.Add(new TemplateHelperExtension(name, render));
        return this;
    }

    /// <summary>Registers a response transformer applied after a response is rendered.</summary>
    public MockifyrExtensions AddResponseTransformer(IResponseTransformer transformer)
    {
        ResponseTransformers.Add(transformer);
        return this;
    }
}
