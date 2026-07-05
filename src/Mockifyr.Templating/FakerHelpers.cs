using Bogus;
using HandlebarsDotNet;

namespace Mockifyr.Templating;

/// <summary>
/// The Faker/<c>random</c> templating helper (G15): <c>{{random 'Class.method'}}</c> renders fake data
/// from a Datafaker-style expression, mirroring WireMock's faker extension. The reference is backed by
/// Datafaker; Mockifyr uses <see cref="Faker"/> (Bogus, Datafaker's .NET counterpart). The output is
/// non-deterministic, so it is validated <em>structurally</em> (format contract per expression), the
/// same method the random helpers use. A curated subset of the most common providers is supported;
/// an unknown expression yields WireMock's own error string.
/// </summary>
internal static class FakerHelpers
{
    public static void Register(IHandlebars handlebars) =>
        handlebars.RegisterHelper("random", (_, arguments) => Evaluate(Argument(arguments)));

    // Datafaker expression ("Class.method") -> Bogus. A fresh Faker per call keeps it thread-safe.
    private static readonly Dictionary<string, Func<Faker, string>> Providers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Name.firstName"] = f => f.Name.FirstName(),
        ["Name.lastName"] = f => f.Name.LastName(),
        ["Name.fullName"] = f => f.Name.FullName(),
        ["Name.name"] = f => f.Name.FullName(),
        ["Internet.emailAddress"] = f => f.Internet.Email(),
        ["Internet.url"] = f => f.Internet.Url(),
        ["Internet.uuid"] = f => f.Random.Guid().ToString(),
        ["Address.city"] = f => f.Address.City(),
        ["Address.country"] = f => f.Address.Country(),
        ["Address.zipCode"] = f => f.Address.ZipCode(),
        ["Number.digit"] = f => f.Random.Int(0, 9).ToString(),
        ["Company.name"] = f => f.Company.CompanyName(),
        ["Lorem.word"] = f => f.Lorem.Word(),
        ["PhoneNumber.phoneNumber"] = f => f.Phone.PhoneNumber(),
    };

    private static object Evaluate(string expression) =>
        Providers.TryGetValue(expression, out var provider)
            ? provider(new Faker())
            : $"[ERROR: Unable to evaluate the expression {expression}]";

    private static string Argument(Arguments arguments) =>
        arguments.Length > 0 && arguments[0] is not null ? arguments[0].ToString() ?? string.Empty : string.Empty;
}
