using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Tests.Analysis.Inventory;
using Csharp2Md.Core.Tests.Detection.Contracts;
using Csharp2Md.Core.Tests.Manifests;
using Csharp2Md.Core.Tests.Projection.Aggregates;
using Csharp2Md.Core.Tests.Projection.Markdown;
using Csharp2Md.Core.Tests.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class MigrationLedgerTests
{
    public static IEnumerable<object[]> BaselineCases() =>
        File.ReadLines(Path.Combine(TestPaths.RepoRoot, ".specs", "features", "csharp2md-v3", "test-migration.md"))
            .Where(static line => line.StartsWith("| ", StringComparison.Ordinal) && char.IsDigit(line[2]))
            .Select(static line => line.Split('|', StringSplitOptions.TrimEntries))
            .Select(static columns => new object[] { columns[1], columns[2], EvidenceFor(columns[2]) });

    [Theory]
    [MemberData(nameof(BaselineCases))]
    public void BaselineBehavior_HasAnExecutableV3Replacement(string baselineId, string baselineTest, MigrationEvidence evidence)
    {
        Assert.NotEmpty(baselineId);
        Assert.StartsWith("Csharp2Md.Core.Tests.", baselineTest, StringComparison.Ordinal);
        Assert.NotNull(evidence.TestType.GetMethod(evidence.MethodName));
    }

    [Fact]
    public void RemovedV2Paths_AreAbsentFromTheProductionAssembly()
    {
        var assembly = typeof(AnalysisEngine).Assembly;

        Assert.All(
            new[]
            {
                "Csharp2Md.Core.Pipeline.AnalysisPipeline",
                "Csharp2Md.Core.Loading.SolutionLoader",
                "Csharp2Md.Core.Rendering.SemanticEnricher",
                "Csharp2Md.Core.Rendering.DependencySectionRenderer",
                "Csharp2Md.Core.Detection.IDocumentDependencyDetector",
                "Csharp2Md.Core.Detection.IProjectDependencyDetector",
                "Csharp2Md.Core.Graph.DependencySignal",
                "Csharp2Md.Core.Graph.GraphBuilder",
                "Csharp2Md.Core.Output.DependencyJsonWriter",
            },
            legacyType => Assert.Null(assembly.GetType(legacyType, throwOnError: false)));
    }

    private static MigrationEvidence EvidenceFor(string baselineTest) => baselineTest.Split('.')[3] switch
    {
        "Cli" => new(typeof(Cli.V3CliRoutingTests), nameof(Cli.V3CliRoutingTests.ZeroOptions_UsesSyntaxOnlyUntrustedAndProducesV3WithoutDotnetOnPath)),
        "Configuration" => new(typeof(Configuration.ConfigIndexerTests), nameof(Configuration.ConfigIndexerTests.Index_AppSettingsWithServicesSection_IndexesLogicalNames)),
        "Detection" => new(typeof(FactualDetectorContractTests), nameof(FactualDetectorContractTests.ProjectAndDocumentDetectors_AreIndependentGranularityContracts)),
        "Discovery" => new(typeof(InertInventoryTests), nameof(InertInventoryTests.Inventory_SyntaxOnly_DoesNotInvokeExecutableAnalysis)),
        "FilterSyntaxProbeTests" => new(typeof(FilterSyntaxProbeTests), nameof(FilterSyntaxProbeTests.IntegrationTaggedTest_HasCategoryIntegration)),
        "Graph" => new(typeof(RelationProjectorTests), nameof(RelationProjectorTests.Writer_ProjectedOutputsMatchApprovedSnapshot)),
        "Loading" => new(typeof(TrustedSemanticAnalysisEngineTests), nameof(TrustedSemanticAnalysisEngineTests.SyntaxOnlyMode_DoesNotInvokeAnySemanticAdapter)),
        "Manifests" => new(typeof(ManifestLoaderTests), nameof(ManifestLoaderTests.Load_ValidManifestWithSingleEntry_ReturnsSuccessWithParsedPath)),
        "Output" => new(typeof(CanonicalAggregateWriterTests), nameof(CanonicalAggregateWriterTests.Write_ManifestMatchesApprovedSnapshot)),
        "Pipeline" => new(typeof(AnalysisEngineTests), nameof(AnalysisEngineTests.AnalyzeAsync_DefaultSyntaxOnly_WritesFactsAndMarkdown)),
        "Rendering" => new(typeof(MarkdownProjectorTests), nameof(MarkdownProjectorTests.Project_AcceptsFactsOnlyAndPreservesEverySectionPayload)),
        "Topic" => new(typeof(TopicOptionsTests), nameof(TopicOptionsTests.Create_AcceptsPlainSlug)),
        _ => throw new InvalidOperationException($"No v3 migration evidence is defined for '{baselineTest}'."),
    };

    public sealed record MigrationEvidence(Type TestType, string MethodName);
}
