using Csharp2Md.Core.Output;
using Csharp2Md.Core.Topic;
using YamlDotNet.Serialization;

namespace Csharp2Md.Core.Tests.Pipeline;

/// <summary>
/// WIKI-06/WIKI-09/WIKI-13: every rendered source document carries frontmatter derived from the
/// syntax tree already materialized for the detectors in <c>AnalyzeDocumentAsync</c> — no second
/// parse, no re-read of a written file — and derivation is syntax-only, so a degraded project (no
/// restore, no semantic model) classifies identically to a healthy one.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FrontmatterDerivationTests(SyntheticFixtureRun run) : IClassFixture<SyntheticFixtureRun>
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder().Build();

    [Fact]
    public void RunAsync_EveryGeneratedSourceDocument_CarriesAParseableFrontmatterBlock()
    {
        var documents = GeneratedDocuments(run.CodebaseRoot);
        Assert.NotEmpty(documents);

        Assert.All(documents, path =>
        {
            var block = ExtractFrontmatterBlock(File.ReadAllText(path));

            Assert.Null(FrontmatterYaml.Validate(block, path));
        });
    }

    // spec.md Edge Cases (WIKI-13): "Acme.Payments is this case: PaymentsService : Payments.PaymentsBase
    // classifies as service with no restore, since the rule reads the declared name." AnalysisPipelineTests
    // already pins Acme.Payments as PossibleMissingRestore/degraded; this test asserts the frontmatter
    // that same degraded run produces matches spec.md's Fixture Expectations table row exactly, not just
    // that some block exists.
    [Fact]
    public void RunAsync_DegradedProjectDocument_DerivesTheSameClassificationAHealthyProjectWould()
    {
        var path = Path.Combine(run.ServiceOutput(SyntheticFixtureRun.Payments), "PaymentsService.cs.md");
        var block = ExtractFrontmatterBlock(File.ReadAllText(path));
        var parsed = Deserializer.Deserialize<Dictionary<string, object>>(StripDelimiters(block));

        Assert.Equal("PaymentsService", parsed["title"]);
        Assert.Equal("service", parsed["file_type"]);
    }

    [Fact]
    public void RunAsync_DocumentCountUnderCodebaseRoot_StillEqualsTheSourceDocumentCount()
    {
        var expected = new[]
            {
                SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts,
            }
            .Sum(service => FixtureManifest.ExpectedSourceFiles(TestPaths.SyntheticSolution(service)).Count);

        Assert.Equal(expected, GeneratedDocuments(run.CodebaseRoot).Count);
    }

    private static IReadOnlyList<string> GeneratedDocuments(string codebaseRoot) =>
        Directory.EnumerateFiles(codebaseRoot, "*.md", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != IndexWriter.FileName)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    // Mirrors RenderedDocumentFrontmatterTests' own delimiter search: the block is everything from
    // the file's first line through the matching closing "---" line.
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
