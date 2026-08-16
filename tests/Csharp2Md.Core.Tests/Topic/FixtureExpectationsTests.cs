using Csharp2Md.Core.Tests.Pipeline;
using YamlDotNet.Serialization;

namespace Csharp2Md.Core.Tests.Topic;

/// <summary>
/// WIKI-23: the committed expectations file spec.md's Fixture Expectations table requires, plus the
/// tests proving every <c>file_type</c> value (11), every tag rule (6), and every title tier (4) is
/// exercised somewhere in <c>fixtures/SyntheticSolution</c>, and that every fixture document
/// classifies exactly as spec.md's table says. Reuses <see cref="SyntheticFixtureRun"/> — the shared
/// full-pipeline run — rather than opening a second <c>MSBuildWorkspace</c>.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FixtureExpectationsTests(SyntheticFixtureRun run) : IClassFixture<SyntheticFixtureRun>
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    /// <summary>
    /// One row of spec.md's Fixture Expectations table. <see cref="TitleTier"/> is not itself a
    /// frontmatter field (<c>Frontmatter</c> carries no tier) — it records which of the four
    /// title-resolution tiers this document exercises, so the tier-coverage assertion below has
    /// something to check against.
    /// </summary>
    public sealed record Expectation(
        string Service, string RelativeSourcePath, string Title, int TitleTier, string FileType, string[] Tags)
    {
        public override string ToString() => $"{Service}/{RelativeSourcePath}";
    }

    // spec.md's Fixture Expectations table, verbatim.
    public static readonly Expectation[] Expectations =
    [
        new(SyntheticFixtureRun.Orders, "Program.cs", "Program", 1, "configuration", ["bootstrapping", "dependency-injection"]),
        new(SyntheticFixtureRun.Orders, "Hosting/ServiceCollectionExtensions.cs", "ServiceCollectionExtensions", 1, "extension", ["dependency-injection"]),
        new(SyntheticFixtureRun.Orders, "Api/OrdersController.cs", "OrdersController", 1, "controller", ["api-endpoint"]),
        new(SyntheticFixtureRun.Orders, "Api/SwaggerOperationDefaultsFilter.cs", "SwaggerOperationDefaultsFilter", 1, "filter", []),
        new(SyntheticFixtureRun.Orders, "Data/OrderDbContext.cs", "OrderDbContext", 1, "data-access", ["persistence"]),
        new(SyntheticFixtureRun.Orders, "Events/OrderPlacedEventHandler.cs", "OrderPlacedEventHandler", 1, "handler", ["event-driven", "async-patterns"]),
        new(SyntheticFixtureRun.Orders, "OrderService.cs", "OrderService", 1, "service", ["event-driven", "async-patterns"]),
        new(SyntheticFixtureRun.Orders, "PaymentsGrpcClient.cs", "PaymentsClient", 2, "class", ["async-patterns"]),
        new(SyntheticFixtureRun.Orders, "Properties/AssemblyInfo.cs", "AssemblyInfo", 4, "class", []),
        new(SyntheticFixtureRun.Payments, "PaymentsService.cs", "PaymentsService", 1, "service", ["event-driven", "async-patterns"]),
        new(SyntheticFixtureRun.SharedContracts, "Events.cs", "OrderPlaced", 3, "class", []),
        new(SyntheticFixtureRun.SharedContracts, "IEventBus.cs", "IEventBus", 1, "interface", ["event-driven", "async-patterns"]),
        new(SyntheticFixtureRun.SharedContracts, "OrderStatus.cs", "OrderStatus", 1, "enum", []),
    ];

    // spec.md Frontmatter Schema's file_type Derivation table, verbatim — the same 11 values
    // schemas/frontmatter.schema.json publishes and FrontmatterSchemaSyncTests keeps in sync with FileType.
    private static readonly string[] AllFileTypeValues =
    [
        "configuration", "controller", "handler", "service", "data-access",
        "enum", "interface", "filter", "extension", "class", "index",
    ];

    // spec.md's tags Derivation table, verbatim.
    private static readonly string[] AllTagValues =
    [
        "bootstrapping", "dependency-injection", "event-driven", "persistence", "async-patterns", "api-endpoint",
    ];

    public static IEnumerable<object[]> ExpectationCases() => Expectations.Select(expectation => new object[] { expectation });

    [Theory]
    [MemberData(nameof(ExpectationCases))]
    public void RunAsync_FixtureDocument_MatchesCommittedExpectation(Expectation expectation)
    {
        var documentPath = Path.Combine(run.ServiceOutput(expectation.Service), expectation.RelativeSourcePath + ".md");
        var (title, fileType, tags) = ParseDocumentFrontmatter(documentPath);

        Assert.True(title == expectation.Title, $"{expectation}: expected title '{expectation.Title}', got '{title}'");
        Assert.True(fileType == expectation.FileType, $"{expectation}: expected file_type '{expectation.FileType}', got '{fileType}'");

        var expectedTags = expectation.Tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
        var actualTags = tags.OrderBy(tag => tag, StringComparer.Ordinal).ToArray();
        Assert.True(
            expectedTags.SequenceEqual(actualTags),
            $"{expectation}: expected tags [{string.Join(", ", expectedTags)}], got [{string.Join(", ", actualTags)}]");
    }

    [Fact]
    public void RunAsync_UnionOfDerivedFileTypes_CoversAllElevenEnumMembers()
    {
        var derived = AllGeneratedDocuments().Select(ParseDocumentFrontmatter).Select(document => document.FileType).ToHashSet();

        var missing = AllFileTypeValues.Except(derived).ToArray();
        Assert.True(missing.Length == 0, $"file_type value(s) never exercised by the fixture: [{string.Join(", ", missing)}]");
    }

    [Fact]
    public void RunAsync_UnionOfDerivedTags_CoversAllSixTagRules()
    {
        var derived = AllGeneratedDocuments().Select(ParseDocumentFrontmatter).SelectMany(document => document.Tags).ToHashSet();

        var missing = AllTagValues.Except(derived).ToArray();
        Assert.True(missing.Length == 0, $"tag(s) never exercised by the fixture: [{string.Join(", ", missing)}]");
    }

    [Fact]
    public void Expectations_TitleTiers_CoverAllFourTiers()
    {
        var tiers = Expectations.Select(expectation => expectation.TitleTier).ToHashSet();

        Assert.Equal(new HashSet<int> { 1, 2, 3, 4 }, tiers);

        // Tier 4 (file-name fallback with a warning) needs a document declaring no top-level type at
        // all; spec.md names Properties/AssemblyInfo.cs as the only fixture file that can supply it —
        // Acme.Broken cannot, because a project that fails to compile contributes no documents.
        var tier4 = Assert.Single(Expectations, expectation => expectation.TitleTier == 4);
        Assert.Equal("Properties/AssemblyInfo.cs", tier4.RelativeSourcePath);
    }

    private IEnumerable<string> AllGeneratedDocuments() =>
        Directory.EnumerateFiles(run.CodebaseRoot, "*.md", SearchOption.AllDirectories);

    private static (string Title, string FileType, string[] Tags) ParseDocumentFrontmatter(string path)
    {
        Assert.True(File.Exists(path), $"expected a generated document at {path}");

        var block = ExtractFrontmatterBlock(File.ReadAllText(path));
        var parsed = Deserializer.Deserialize<Dictionary<string, object>>(StripDelimiters(block))!;
        var tags = ((IEnumerable<object>)parsed["tags"]).Cast<string>().ToArray();

        return ((string)parsed["title"], (string)parsed["file_type"], tags);
    }

    // Mirrors FrontmatterDerivationTests' own delimiter search: the block is everything from the
    // file's first line through the matching closing "---" line.
    private static string ExtractFrontmatterBlock(string content)
    {
        Assert.StartsWith("---\n", content, StringComparison.Ordinal);
        var closingDelimiterIndex = content.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        Assert.True(closingDelimiterIndex > 0, "expected a closing '---' delimiter line");

        return content[..(closingDelimiterIndex + "\n---\n".Length)];
    }

    private static string StripDelimiters(string block) =>
        string.Join('\n', block.Split('\n').Where(line => line != "---" && line.Length > 0));
}
