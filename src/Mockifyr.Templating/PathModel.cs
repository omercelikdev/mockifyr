using System;
using System.Collections.Generic;
using System.Globalization;
using HandlebarsDotNet.MemberAccessors;
using HandlebarsDotNet.ObjectDescriptors;
using HandlebarsDotNet.PathStructure;

namespace Mockifyr.Templating;

/// <summary>
/// The dual <c>request.path</c> model: it renders as the full path string when used bare
/// (<c>{{request.path}}</c>), yet exposes members — named path variables from a <c>urlPathTemplate</c>
/// (<c>{{request.path.id}}</c>) and zero-based path segments (<c>{{request.path.0}}</c> /
/// <c>{{request.path.[0]}}</c>). Handlebars.Net renders any enumerable by listing its items, so a
/// plain dictionary can't be this: instead a custom <see cref="IObjectDescriptorProvider"/> makes the
/// type non-enumerable (bare → <see cref="ToString"/>) with a member accessor over the backing map.
/// Missing members render as an empty string. Verified by the differential suite.
/// </summary>
internal sealed class PathModel
{
    private readonly string _full;

    public PathModel(string path, IReadOnlyList<string> segments, IReadOnlyDictionary<string, string>? namedVariables)
    {
        _full = path;
        var members = new Dictionary<string, string>(StringComparer.Ordinal);

        // Zero-based path segments: {{request.path.0}} → first segment.
        for (var i = 0; i < segments.Count; i++)
        {
            members[i.ToString(System.Globalization.CultureInfo.InvariantCulture)] = segments[i];
        }

        // Named variables from urlPathTemplate override/augment: {{request.path.id}}.
        foreach (var (name, value) in namedVariables ?? new Dictionary<string, string>())
        {
            members[name] = value;
        }

        Members = members;
    }

    public IReadOnlyDictionary<string, string> Members { get; }

    /// <summary>The bare form (<c>{{request.path}}</c>) is the full request path.</summary>
    public override string ToString() => _full;

    /// <summary>
    /// Extracts named path variables by aligning a <c>urlPathTemplate</c> (e.g. <c>/users/{id}</c>)
    /// with the actual path (<c>/users/7</c>) segment by segment. Each <c>{name}</c> template segment
    /// binds to the corresponding path segment. Returns empty when there is no template.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ExtractVariables(string? urlPathTemplate, IReadOnlyList<string> pathSegments)
    {
        if (string.IsNullOrEmpty(urlPathTemplate))
        {
            return EmptyVariables;
        }

        var templateSegments = urlPathTemplate.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < templateSegments.Length && i < pathSegments.Count; i++)
        {
            var segment = templateSegments[i];
            if (segment.Length > 2 && segment[0] == '{' && segment[^1] == '}')
            {
                variables[segment[1..^1]] = pathSegments[i];
            }
        }

        return variables;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyVariables =
        new Dictionary<string, string>(StringComparer.Ordinal);
}

/// <summary>Describes <see cref="PathModel"/> to Handlebars.Net as a non-enumerable object with dynamic members.</summary>
internal sealed class PathModelDescriptorProvider : IObjectDescriptorProvider
{
    public bool TryGetDescriptor(Type type, out ObjectDescriptor value)
    {
        if (type != typeof(PathModel))
        {
            value = ObjectDescriptor.Empty;
            return false;
        }

        value = new ObjectDescriptor(
            typeof(PathModel),
            new PathMemberAccessor(),
            getProperties: static (_, instance) => ((PathModel)instance).Members.Keys,
            iterator: static _ => null!, // non-enumerable: bare {{path}} renders via ToString
            dependencies: []);
        return true;
    }
}

/// <summary>Resolves a <see cref="PathModel"/> member; bracketed indices (<c>[0]</c>) map to <c>0</c>.</summary>
internal sealed class PathMemberAccessor : IMemberAccessor
{
    public bool TryGetValue(object instance, ChainSegment memberName, out object value)
    {
        var key = memberName.ToString();
        if (key.Length > 1 && key[0] == '[' && key[^1] == ']')
        {
            key = key[1..^1];
        }

        // Always "found" so an unknown member renders as an empty string, never the type name.
        value = ((PathModel)instance).Members.TryGetValue(key, out var v) ? v : string.Empty;
        return true;
    }
}
