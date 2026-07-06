using Mockifyr.Differential.Generator;
using Mockifyr.Differential.Harness;

namespace Mockifyr.Differential.Tests;

/// <summary>
/// Property-based validation of the standard matchers: the fuzzing generator emits hundreds of
/// (stub, request) probes across the text corpus, and each probe's match/no-match decision must
/// agree with the WireMock oracle (and, when matched, the body must diff green). This is the
/// rigor the brief mandates over hand-picked cases. Requires Docker.
/// </summary>
public sealed class G1GeneratedMatcherTests : IAsyncLifetime
{
    private readonly DifferentialRunner _runner = new();

    public Task InitializeAsync() => _runner.StartAsync();

    public async Task DisposeAsync() => await _runner.DisposeAsync();

    [Fact]
    public Task Url_Pattern() => Verify(UrlScenarios.UrlPattern());

    [Fact]
    public Task Url_PathPattern() => Verify(UrlScenarios.UrlPathPattern());

    [Fact]
    public Task Url_PathTemplate() => Verify(UrlScenarios.UrlPathTemplate());

    [Fact]
    public Task EqualTo_Header() => Verify(MatcherScenarios.EqualTo(Target.Header, seed: 11));

    [Fact]
    public Task EqualTo_Query() => Verify(MatcherScenarios.EqualTo(Target.Query, seed: 12));

    [Fact]
    public Task EqualTo_Body() => Verify(MatcherScenarios.EqualTo(Target.Body, seed: 13));

    [Fact]
    public Task Contains_Header() => Verify(MatcherScenarios.Contains(Target.Header, seed: 21));

    [Fact]
    public Task Contains_Body() => Verify(MatcherScenarios.Contains(Target.Body, seed: 22));

    [Fact]
    public Task Matches_Body() => Verify(MatcherScenarios.Matches(seed: 31));

    [Fact]
    public Task Body_BinaryEqualTo() => Verify(BinaryScenarios.BinaryEqualTo());

    [Fact]
    public Task Absent_Header() => Verify(MatcherScenarios.Absent(Target.Header));

    [Fact]
    public Task Absent_Query() => Verify(MatcherScenarios.Absent(Target.Query));

    // Cookie presence (absent) matches the oracle; cookie VALUE matching (equalTo/contains)
    // shows a normalization divergence that is deferred to a focused pass — see
    // docs/parity/g1-matching.md.
    [Fact]
    public Task Absent_Cookie() => Verify(MatcherScenarios.Absent(Target.Cookie));

    [Fact]
    public Task EqualTo_Cookie() => Verify(MatcherScenarios.EqualTo(Target.Cookie, seed: 41));

    [Fact]
    public Task Contains_Cookie() => Verify(MatcherScenarios.Contains(Target.Cookie, seed: 42));

    [Fact]
    public Task MultiValue_HasExactly() => Verify(MultiValueScenarios.HasExactly());

    [Fact]
    public Task MultiValue_Includes() => Verify(MultiValueScenarios.Includes());

    [Fact]
    public Task DoesNotMatch_Header() => Verify(MatcherScenarios.DoesNotMatch(Target.Header));

    [Fact]
    public Task DoesNotMatch_Body() => Verify(MatcherScenarios.DoesNotMatch(Target.Body));

    [Fact]
    public Task EqualToIgnoreCase_Header() => Verify(MatcherScenarios.EqualToIgnoreCase(Target.Header));

    [Fact]
    public Task EqualToIgnoreCase_Body() => Verify(MatcherScenarios.EqualToIgnoreCase(Target.Body));

    [Fact]
    public Task EqualToJson_Strict() => Verify(JsonScenarios.EqualToJson(ignoreArrayOrder: false, ignoreExtraElements: false));

    [Fact]
    public Task EqualToJson_IgnoreArrayOrder() => Verify(JsonScenarios.EqualToJson(ignoreArrayOrder: true, ignoreExtraElements: false));

    [Fact]
    public Task EqualToJson_IgnoreExtraElements() => Verify(JsonScenarios.EqualToJson(ignoreArrayOrder: false, ignoreExtraElements: true));

    [Fact]
    public Task EqualToJson_IgnoreBoth() => Verify(JsonScenarios.EqualToJson(ignoreArrayOrder: true, ignoreExtraElements: true));

    [Fact]
    public Task EqualToJson_Edges() => Verify(JsonScenarios.Edges());

    [Fact]
    public Task MatchesJsonPath_Presence() => Verify(JsonPathScenarios.Presence());

    [Fact]
    public Task MatchesJsonPath_SubMatcher() => Verify(JsonPathScenarios.SubMatcher());

    [Fact]
    public Task MatchesJsonPath_NumericFilters() => Verify(JsonPathScenarios.NumericFilters());

    [Fact]
    public Task MatchesJsonPath_StringFilters() => Verify(JsonPathScenarios.StringFilters());

    [Fact]
    public Task MatchesJsonSchema_InlineObject() => Verify(JsonSchemaScenarios.InlineObject());

    [Fact]
    public Task MatchesJsonSchema_StringFormAndVersion() => Verify(JsonSchemaScenarios.StringFormAndVersion());

    [Fact]
    public Task MatchesJsonSchema_Format() => Verify(JsonSchemaScenarios.Format());

    [Fact]
    public Task MatchesJsonSchema_TypeLoose() => Verify(JsonSchemaScenarios.TypeLoose());

    [Fact]
    public Task MatchesJsonSchema_Ref() => Verify(JsonSchemaScenarios.Ref());

    [Fact]
    public Task EqualToXml() => Verify(XmlScenarios.EqualToXml());

    [Fact]
    public Task MatchesXPath() => Verify(XmlScenarios.MatchesXPath());

    [Fact]
    public Task MatchesXPath_Namespaces() => Verify(XmlScenarios.NamespacedXPath());

    [Fact]
    public Task EqualToXml_Placeholders() => Verify(XmlScenarios.XmlPlaceholders());

    [Fact]
    public Task MatchesXPath_Functions() => Verify(XmlScenarios.XPathFunctions());

    [Fact]
    public Task Xml_Edges() => Verify(XmlScenarios.XmlEdges());

    [Fact]
    public Task DoesNotContain() => Verify(MatchingGapScenarios.DoesNotContain());

    [Fact]
    public Task FormParameters() => Verify(MatchingGapScenarios.FormParameters());

    [Fact]
    public Task Logic_AndOrNot() => Verify(LogicScenarios.AndOrNot());

    [Fact]
    public Task BasicAuth() => Verify(AuthScenarios.BasicAuth());

    [Fact]
    public Task Selection_Priority() => Verify(SelectionScenarios.Priority());

    [Fact]
    public Task Multipart() => Verify(MultipartScenarios.Multipart());

    [Fact]
    public Task DateTime_Comparisons() => Verify(DateTimeScenarios.Comparisons());

    [Fact]
    public Task DateTime_ActualFormat() => Verify(DateTimeScenarios.ActualFormat());

    private async Task Verify(IEnumerable<MatcherScenario> scenarios)
    {
        var failures = new List<string>();
        var matched = 0;
        var unmatched = 0;

        foreach (var scenario in scenarios)
        {
            await _runner.LoadAsync(scenario.WireMockJson);

            foreach (var probe in scenario.Probes)
            {
                var outcome = await _runner.ProbeAsync(probe.Request);

                if (outcome.OracleMatched)
                {
                    matched++;
                }
                else
                {
                    unmatched++;
                }

                if (!outcome.DecisionAgrees)
                {
                    failures.Add(
                        $"{scenario.Description}: decision mismatch — oracle matched={outcome.OracleMatched}, " +
                        $"mockifyr matched={outcome.MockifyrMatched} (statuses {outcome.Oracle.Status}/{outcome.Mockifyr.Status})");
                }
                else if (outcome.OracleMatched && !outcome.Diff.IsMatch)
                {
                    failures.Add($"{scenario.Description}: body diff — {outcome.Diff.Report}");
                }
            }
        }

        // The generator must exercise both outcomes, otherwise the assertion is vacuous.
        Assert.True(matched > 0 && unmatched > 0, $"degenerate coverage: matched={matched}, unmatched={unmatched}");
        Assert.True(failures.Count == 0, $"{failures.Count} divergence(s):\n{string.Join("\n", failures.Take(25))}");
    }
}
