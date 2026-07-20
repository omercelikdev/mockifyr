using Mockifyr.Core;

namespace Mockifyr.Core.Tests;

/// <summary>
/// Unit coverage for the environment substitution pass (G17, issues #165/#166). This is pure logic the
/// differential oracle cannot judge — WireMock has no environments concept — so it is pinned here
/// instead. The load-bearing property is <em>selectivity</em>: the pass shares the <c>{{…}}</c> surface
/// with Handlebars, so it must replace defined keys and leave every other construct byte-identical.
/// </summary>
public class EnvironmentSubstitutionTests
{
    private static EnvironmentSubstitution.TryResolve Keys(params (string Key, string Value)[] pairs) =>
        (string key, out string value) =>
        {
            foreach (var (k, v) in pairs)
            {
                if (string.Equals(k, key, StringComparison.Ordinal))
                {
                    value = v;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        };

    [Fact]
    public void Substitutes_a_defined_key()
    {
        var result = EnvironmentSubstitution.Apply("{{baseUrl}}/users", Keys(("baseUrl", "https://dev.example.com")));

        Assert.Equal("https://dev.example.com/users", result);
    }

    [Fact]
    public void Substitutes_every_occurrence_and_several_keys()
    {
        var result = EnvironmentSubstitution.Apply(
            "{{baseUrl}}/{{region}}/a/{{baseUrl}}",
            Keys(("baseUrl", "X"), ("region", "eu")));

        Assert.Equal("X/eu/a/X", result);
    }

    [Fact]
    public void Tolerates_surrounding_whitespace_in_the_reference()
    {
        var result = EnvironmentSubstitution.Apply("{{ baseUrl }}", Keys(("baseUrl", "X")));

        Assert.Equal("X", result);
    }

    // The selectivity contract: anything that is not a defined key must survive untouched, because
    // Handlebars renders it afterwards. A regex that ate every {{…}} would blank these.
    [Theory]
    [InlineData("{{now}}")]
    [InlineData("{{request.path}}")]
    [InlineData("{{random 'Name.fullName'}}")]
    [InlineData("{{#each items}}{{this}}{{/each}}")]
    [InlineData("{{jsonPath request.body '$.id'}}")]
    [InlineData("{{undefinedKey}}")]
    [InlineData("{{ two words }}")]
    [InlineData("no braces at all")]
    [InlineData("{{unclosed")]
    public void Leaves_everything_that_is_not_a_defined_key_untouched(string input)
    {
        var result = EnvironmentSubstitution.Apply(input, Keys(("baseUrl", "X")));

        Assert.Equal(input, result);
    }

    [Fact]
    public void Returns_the_same_instance_when_nothing_matches()
    {
        const string input = "{{now}} and {{request.method}}";

        Assert.Same(input, EnvironmentSubstitution.Apply(input, Keys(("baseUrl", "X"))));
    }

    [Fact]
    public void Key_lookup_is_case_sensitive_so_a_near_miss_falls_through_to_handlebars()
    {
        var result = EnvironmentSubstitution.Apply("{{BaseUrl}}", Keys(("baseUrl", "X")));

        Assert.Equal("{{BaseUrl}}", result);
    }

    // A substituted value is inserted verbatim and never rescanned, so a value that itself looks like
    // a reference cannot trigger a second round of substitution.
    [Fact]
    public void Does_not_recursively_expand_a_substituted_value()
    {
        var result = EnvironmentSubstitution.Apply("{{a}}", Keys(("a", "{{b}}"), ("b", "boom")));

        Assert.Equal("{{b}}", result);
    }

    [Fact]
    public void Substitutes_inside_a_json_body_without_disturbing_the_template_expressions()
    {
        var result = EnvironmentSubstitution.Apply(
            """{"url":"{{baseUrl}}/v1","at":"{{now}}","id":"{{request.path.[0]}}"}""",
            Keys(("baseUrl", "https://api.test")));

        Assert.Equal("""{"url":"https://api.test/v1","at":"{{now}}","id":"{{request.path.[0]}}"}""", result);
    }

    [Fact]
    public void Resolve_returns_the_active_value()
    {
        var key = new EnvironmentKey("baseUrl", "dev",
            [new EnvironmentValue("local", "http://localhost"), new EnvironmentValue("dev", "https://dev")]);

        Assert.Equal("https://dev", key.Resolve());
    }

    [Fact]
    public void Resolve_returns_null_when_the_active_value_no_longer_exists()
    {
        var key = new EnvironmentKey("baseUrl", "deleted", [new EnvironmentValue("local", "http://localhost")]);

        Assert.Null(key.Resolve());
    }

    [Theory]
    [InlineData("now")]
    [InlineData("NOW")]
    [InlineData("request")]
    [InlineData("random")]
    [InlineData("each")]
    [InlineData("jwt")]
    public void Reserved_keys_are_rejected(string key) => Assert.True(ReservedEnvironmentKeys.IsReserved(key));

    [Theory]
    [InlineData("baseUrl")]
    [InlineData("region")]
    [InlineData("auth_token")]
    [InlineData("my-key")]
    public void Ordinary_keys_are_not_reserved(string key) => Assert.False(ReservedEnvironmentKeys.IsReserved(key));

    [Theory]
    [InlineData("baseUrl", true)]
    [InlineData("_private", true)]
    [InlineData("a-b_c9", true)]
    [InlineData("", false)]
    [InlineData("9lives", false)]
    [InlineData("has space", false)]
    [InlineData("has.dot", false)]
    public void Well_formedness_matches_what_the_substitution_pass_can_actually_resolve(string key, bool expected) =>
        Assert.Equal(expected, ReservedEnvironmentKeys.IsWellFormed(key));

    // Guards the reserved-list/well-formed pairing: every reserved name is itself a legal key shape,
    // so the reserved check is the only thing standing between a user and a shadowed helper.
    [Fact]
    public void Every_reserved_name_would_otherwise_be_a_substitutable_key()
    {
        foreach (var name in new[] { "now", "request", "random", "each", "if", "base64", "uuid" })
        {
            Assert.True(ReservedEnvironmentKeys.IsWellFormed(name), name);
        }
    }
}
