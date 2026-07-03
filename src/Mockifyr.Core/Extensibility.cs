namespace Mockifyr.Core;

// Extension seams. The built-in features are themselves implementations of these interfaces
// (dogfooding), so the extension mechanism is real from G1 rather than bolted on later. They
// are made public/registerable at roadmap item G10. See ARCHITECTURE.md section 9.

/// <summary>Base marker for an extension with an optional lifecycle.</summary>
public interface IExtension
{
    /// <summary>Called once when the engine starts.</summary>
    void Start() { }

    /// <summary>Called once when the engine stops.</summary>
    void Stop() { }
}

/// <summary>Transforms a rendered response. The built-in <c>response-template</c> is one of these.</summary>
public interface IResponseTransformer : IExtension
{
    /// <summary>Unique transformer name, referenced from stub JSON.</summary>
    string Name { get; }

    /// <summary>Whether the transformer applies to every response by default.</summary>
    bool ApplyGlobally => true;

    /// <summary>Transforms the response for the given serve event.</summary>
    CanonicalResponse Transform(CanonicalResponse response, ServeEvent serveEvent);
}

/// <summary>Transforms a <see cref="ResponseDefinition"/> before it is rendered.</summary>
public interface IResponseDefinitionTransformer : IExtension
{
    /// <summary>Unique transformer name.</summary>
    string Name { get; }

    /// <summary>Transforms the definition for the given serve event.</summary>
    ResponseDefinition Transform(ResponseDefinition definition, ServeEvent serveEvent);
}

/// <summary>A single templating helper (e.g. <c>jsonPath</c>, <c>now</c>, <c>randomValue</c>).</summary>
public interface ITemplateHelper
{
    /// <summary>The helper name as used in a template.</summary>
    string Name { get; }
}

/// <summary>
/// A user-supplied template helper (G10): its <see cref="ITemplateHelper.Name"/> plus a render
/// function over the positional arguments. The templating edge adapts it to the underlying engine, so
/// the public API stays engine-agnostic.
/// </summary>
public sealed record TemplateHelperExtension(string Name, Func<IReadOnlyList<object?>, string> Render) : ITemplateHelper;

/// <summary>Contributes one or more <see cref="ITemplateHelper"/> instances.</summary>
public interface ITemplateHelperProvider : IExtension
{
    /// <summary>The helpers this provider contributes.</summary>
    IReadOnlyList<ITemplateHelper> GetHelpers();
}

/// <summary>Contributes extra data to the template model beyond the request.</summary>
public interface ITemplateModelProvider : IExtension
{
    /// <summary>Contributes model data for the given serve event.</summary>
    IReadOnlyDictionary<string, object?> GetModelData(ServeEvent serveEvent);
}

/// <summary>Resolves named custom matchers referenced from stub JSON.</summary>
public interface IMatcherRegistry
{
    /// <summary>Registers a named matcher factory.</summary>
    void Register(string name, IMatcher matcher);

    /// <summary>Resolves a matcher by name, or null when unknown.</summary>
    IMatcher? Resolve(string name);
}

/// <summary>Adds custom endpoints under <c>/__admin/ext/*</c>.</summary>
public interface IAdminApiExtension : IExtension
{
    /// <summary>The route prefix this extension serves.</summary>
    string RoutePrefix { get; }
}

/// <summary>Loads stub mappings from a source (e.g. a mappings directory) into a tenant.</summary>
public interface IMappingsLoader : IExtension
{
    /// <summary>Loads mappings for the given tenant.</summary>
    IReadOnlyList<StubMapping> Load(TenantId tenant);
}

/// <summary>Filters or short-circuits requests before matching.</summary>
public interface IRequestFilter : IExtension
{
    /// <summary>The filter name.</summary>
    string Name { get; }
}
