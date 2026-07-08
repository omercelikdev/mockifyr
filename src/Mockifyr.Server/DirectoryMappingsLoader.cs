using Mockifyr.Adapters.MappingJson;
using Mockifyr.Core;

namespace Mockifyr.Server;

/// <summary>
/// Loads stub mappings from a directory of WireMock JSON files at startup (G12f) — WireMock's
/// <c>&lt;root-dir&gt;/mappings/*.json</c> convention. Each file is a single stub or a
/// <c>{"mappings":[…]}</c> bundle; files are read in filename order for deterministic ids/priority
/// tie-breaks. This is the <see cref="IMappingsLoader"/> extension seam (public since G10), wired
/// here for the standalone host. It does file I/O, so it lives at the host edge — never in Core.
/// </summary>
public sealed class DirectoryMappingsLoader(string mappingsDirectory, IMatcherRegistry? matchers = null) : IMappingsLoader
{
    /// <inheritdoc />
    public IReadOnlyList<StubMapping> Load(TenantId tenant)
    {
        if (!Directory.Exists(mappingsDirectory))
        {
            return [];
        }

        var stubs = new List<StubMapping>();
        foreach (var file in Directory.EnumerateFiles(mappingsDirectory, "*.json").OrderBy(path => path, StringComparer.Ordinal))
        {
            stubs.AddRange(MappingJsonReader.Read(File.ReadAllText(file), tenant, matchers));
        }

        return stubs;
    }
}
