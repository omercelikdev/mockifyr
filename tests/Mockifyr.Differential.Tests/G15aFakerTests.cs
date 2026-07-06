using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Validation of the Faker <c>random</c> helper (G15a). Its output is non-deterministic, so it can't
/// be byte-diffed — it is validated <em>structurally</em> (the racy-feature method): the same faker
/// stub is served by the oracle (WireMock + the faker extension) and Mockifyr, and over many iterations
/// each generated field must satisfy a format contract on <b>both</b> sides. The oracle satisfying the
/// contract proves it is real WireMock/Datafaker behavior; Mockifyr (Bogus) satisfying the same
/// contract is the parity claim. Requires Docker.
/// </summary>
public sealed class G15aFakerTests : IAsyncLifetime
{
    private const string Template =
        "firstName=[{{random 'Name.firstName'}}]|lastName=[{{random 'Name.lastName'}}]|" +
        "fullName=[{{random 'Name.fullName'}}]|email=[{{random 'Internet.emailAddress'}}]|" +
        "city=[{{random 'Address.city'}}]|zip=[{{random 'Address.zipCode'}}]|" +
        "digit=[{{random 'Number.digit'}}]|uuid=[{{random 'Internet.uuid'}}]|word=[{{random 'Lorem.word'}}]|" +
        // Long-tail providers (G15e): each still has a stable-enough structural contract satisfied by
        // both the oracle (Datafaker) and Mockifyr (Bogus).
        "username=[{{random 'Name.username'}}]|prefix=[{{random 'Name.prefix'}}]|" +
        "domain=[{{random 'Internet.domainName'}}]|ipv4=[{{random 'Internet.ipV4Address'}}]|" +
        "mac=[{{random 'Internet.macAddress'}}]|state=[{{random 'Address.state'}}]|" +
        "street=[{{random 'Address.streetAddress'}}]|product=[{{random 'Commerce.productName'}}]|" +
        "sentence=[{{random 'Lorem.sentence'}}]";

    private static readonly string MappingJson =
        "{\"request\":{\"method\":\"GET\",\"urlPath\":\"/faker\"}," +
        "\"response\":{\"status\":200,\"transformers\":[\"response-template\"],\"body\":\"" + Template + "\"}}";

    private readonly WireMockFakerOracle _oracle = new();
    private readonly WebApplicationFactory<Program> _mockifyr = new();

    public Task InitializeAsync() => _oracle.StartAsync();

    public async Task DisposeAsync()
    {
        await _mockifyr.DisposeAsync();
        await _oracle.DisposeAsync();
    }

    private sealed record Field(string Name, Regex Contract);

    private static readonly Field[] Fields =
    [
        new("firstName", new Regex(@"^[\p{L}'.\- ]+$", RegexOptions.Compiled)),
        new("lastName", new Regex(@"^[\p{L}'.\- ]+$", RegexOptions.Compiled)),
        new("fullName", new Regex(@"^[\p{L}'.\- ]+$", RegexOptions.Compiled)),
        new("email", new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled)),
        new("city", new Regex(@"^[\p{L}'.\- ]+$", RegexOptions.Compiled)),
        new("zip", new Regex(@"^\d{5}(-\d{4})?$", RegexOptions.Compiled)),
        new("digit", new Regex(@"^\d$", RegexOptions.Compiled)),
        new("uuid", new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$", RegexOptions.Compiled)),
        new("word", new Regex(@"^\p{L}+$", RegexOptions.Compiled)),
        // Long-tail (G15e). Contracts are loose enough for both Datafaker and Bogus, tight enough to
        // catch a wrong provider (an IP that isn't dotted-quad, a MAC that isn't colon-hex, etc.).
        new("username", new Regex(@"^[\p{L}\d._-]+$", RegexOptions.Compiled)),
        new("prefix", new Regex(@"^[\p{L}.\- ]+$", RegexOptions.Compiled)),
        new("domain", new Regex(@"^[\p{L}\d.\-]+\.[\p{L}]+$", RegexOptions.Compiled)),
        new("ipv4", new Regex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.Compiled)),
        new("mac", new Regex(@"^([0-9a-fA-F]{2}:){5}[0-9a-fA-F]{2}$", RegexOptions.Compiled)),
        new("state", new Regex(@"^[\p{L}.\- ]+$", RegexOptions.Compiled)),
        new("street", new Regex(@"^[\p{L}\d'.,\-# ]+$", RegexOptions.Compiled)),
        new("product", new Regex(@"^[\p{L} ]+$", RegexOptions.Compiled)),
        new("sentence", new Regex(@"^[\p{L} ,.'-]+$", RegexOptions.Compiled)),
    ];

    [Fact]
    public async Task FakerHelper_StructurallyMatchesTheOracle()
    {
        using var mockifyrClient = _mockifyr.CreateClient();
        await LoadAsync(_oracle.Client);
        await LoadAsync(mockifyrClient);

        var failures = new List<string>();
        for (var iteration = 0; iteration < 15; iteration++)
        {
            var oracle = await _oracle.Client.GetStringAsync("/faker");
            var mockifyr = await mockifyrClient.GetStringAsync("/faker");

            foreach (var field in Fields)
            {
                var oracleValue = Extract(oracle, field.Name);
                var mockifyrValue = Extract(mockifyr, field.Name);

                if (!field.Contract.IsMatch(oracleValue))
                {
                    failures.Add($"{field.Name}: ORACLE value violates the contract: \"{oracleValue}\"");
                }

                if (!field.Contract.IsMatch(mockifyrValue))
                {
                    failures.Add($"{field.Name}: mockifyr value violates the contract: \"{mockifyrValue}\"");
                }
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} faker divergence(s):\n{string.Join("\n", failures.Distinct())}");
    }

    private static async Task LoadAsync(HttpClient client)
    {
        await client.PostAsync("/__admin/mappings/reset", content: null);
        using var content = new StringContent(MappingJson, Encoding.UTF8, "application/json");
        await client.PostAsync("/__admin/mappings", content);
    }

    private static string Extract(string body, string field)
    {
        var match = Regex.Match(body, Regex.Escape(field) + @"=\[(.*?)\]");
        return match.Success ? match.Groups[1].Value : "<absent>";
    }
}
