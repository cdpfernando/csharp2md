using System.Text.Json;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.Core.Tests.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Analysis;

/// <summary>
/// Spec.md's five P1 Independent Tests, restated verbatim against a real default-mode (syntax-only,
/// untrusted) <see cref="AnalysisEngine.AnalyzeAsync"/> run over <c>fixtures/SyntheticSolution</c>,
/// mirroring <c>DataAccessDiscoveryEndToEndTests</c>'s structure.
/// </summary>
[Trait("Category", "Integration")]
public sealed class RelationResolverEndToEndTests(RelationResolverEndToEndFixture fixture)
    : IClassFixture<RelationResolverEndToEndFixture>
{
    // "the calls relation whose source is OrderService's method carries a target_id equal to the
    // SymbolFactId of the PaymentClient.Authorize method declared in another project, with
    // resolution_method set to syntactic."
    [Fact]
    public void RELR01_CrossProjectCallsRelation_CarriesThePaymentClientAuthorizeSymbolIdAndSyntactic()
    {
        var relation = Assert.Single(
            fixture.StructuralRelations,
            r => r.RelationKind == "calls" && Detail(r, "target_text") == "crossProjectPaymentClient.Authorize");

        Assert.Equal("syntactic", relation.ResolutionMethod);
        Assert.NotNull(relation.TargetId);
        Assert.Contains("PaymentClient", relation.TargetId, StringComparison.Ordinal);
        Assert.Contains("Authorize", relation.TargetId, StringComparison.Ordinal);
        // Genuinely a different project from OrderService's own Acme.Orders.
        Assert.Contains("Acme.Shared.Contracts", relation.TargetId, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme.Orders", relation.TargetId, StringComparison.Ordinal);
    }

    // "confirm the emitted relation has target_id null, resolution_method set to candidate, both
    // candidate ids listed in candidates, and a C2M-RELR-002 entry in raw/facts/diagnostics.json."
    [Fact]
    public void RELR01_AmbiguousReferenceRelation_CarriesNullTargetCandidateBothIdsAndADiagnostic()
    {
        var relation = Assert.Single(
            fixture.StructuralRelations,
            r => r.RelationKind == "creates" && Detail(r, "target_text") == "AuditRecorder");

        Assert.Null(relation.TargetId);
        Assert.Equal("candidate", relation.ResolutionMethod);
        Assert.NotNull(relation.Candidates);
        Assert.Contains(relation.Candidates!.Value, id => id.Contains("Acme.Orders", StringComparison.Ordinal));
        Assert.Contains(relation.Candidates!.Value, id => id.Contains("Acme.Shared.Contracts", StringComparison.Ordinal));
        Assert.Contains("C2M-RELR-002", fixture.Diagnostics, StringComparison.Ordinal);
    }

    // "Run the CLI twice over the same fixture into two output roots and confirm every
    // raw/facts/relations/*.json file is byte-identical"
    [Fact]
    public void RELR01_TwoRuns_ProduceByteIdenticalRelationPartitionFiles()
    {
        foreach (var wireName in RelationPartitionWireNames)
        {
            var relativePath = Path.Combine("facts", "relations", $"{wireName}.json");
            var a = File.ReadAllBytes(Path.Combine(TopicLayout.RawRoot(fixture.Output), relativePath));
            var b = File.ReadAllBytes(Path.Combine(TopicLayout.RawRoot(fixture.OutputB), relativePath));
            Assert.Equal(a, b);
        }
    }

    // "the relation targeting the ToTable-configured object carries resolution_method set to
    // configured, the relation targeting the convention-named object carries resolution_method set to
    // convention with the header resolution still Heuristic, and the interpolated-SQL relation carries
    // resolution_method set to dynamic with target_id null."
    [Fact]
    public void RELR01_DatabaseRelations_CarryConfiguredConventionAndDynamicMethodsWithConventionsHeaderStillHeuristic()
    {
        var configured = Assert.Single(
            fixture.DataRelations, r => r.RelationKind == "maps-to" && Detail(r, "mapping") == "configured");
        Assert.Equal("configured", configured.ResolutionMethod);
        Assert.NotNull(configured.TargetId);

        var convention = Assert.Single(
            fixture.DataRelations, r => r.RelationKind == "maps-to" && Detail(r, "mapping") == "convention");
        Assert.Equal("convention", convention.ResolutionMethod);
        Assert.Equal("heuristic", convention.Header.Resolution);
        Assert.Null(convention.TargetId);

        var dynamicAccess = Assert.Single(fixture.DataRelations, r => Detail(r, "target_text") == "dynamic-table");
        Assert.Equal("dynamic", dynamicAccess.ResolutionMethod);
        Assert.Null(dynamicAccess.TargetId);
    }

    // "raw/facts/relations/resolution.json's totals equal the partition files, summed by the test
    // rather than restated."
    [Fact]
    public void RELR01_ResolutionJsonTotals_EqualThePartitionFilesSummedByTheTest()
    {
        var allRelations = fixture.RelationsByPartition.Values.SelectMany(static entries => entries).ToArray();

        Assert.Equal(allRelations.Length, fixture.Resolution.Total);
        Assert.Equal(
            allRelations.Length,
            fixture.Resolution.ByMethod.Exact + fixture.Resolution.ByMethod.Candidate + fixture.Resolution.ByMethod.Syntactic
            + fixture.Resolution.ByMethod.Configured + fixture.Resolution.ByMethod.Convention + fixture.Resolution.ByMethod.Dynamic
            + fixture.Resolution.ByMethod.Heuristic + fixture.Resolution.ByMethod.Unresolved);

        foreach (var (wireName, relations) in fixture.RelationsByPartition)
        {
            var partitionMetrics = Assert.Single(fixture.Resolution.ByPartition, entry => entry.Partition == wireName);
            Assert.Equal(relations.Length, partitionMetrics.Total);
        }
    }

    // spec.md's Success Criteria also asks for "at least one edge outside the data partition" here, but per
    // AD-020 that is unreachable regardless of resolution quality: no production path anywhere mints a
    // ComponentFact, and RelationProjector.Mermaid's componentByProject lookup is keyed by project-shaped
    // FactIds while relation Source/TargetIds are symbol- or document-shaped, so it would still miss even if
    // one did. Both are pre-existing gaps outside this feature's scope (confirmed by a real run: the file is
    // always "flowchart LR\n"). This test proves what relation-resolver itself is responsible for — the file
    // is written — and stops short of the edge assertion by explicit user decision.
    [Fact]
    public void RELR01_DependenciesMermaid_IsWrittenAndNonEmpty()
    {
        var mermaid = File.ReadAllText(Path.Combine(TopicLayout.RawRoot(fixture.Output), "dependencies.mmd"));

        Assert.NotEmpty(mermaid);
        Assert.Contains("flowchart", mermaid, StringComparison.Ordinal);
    }

    private static readonly string[] RelationPartitionWireNames =
        ["compile-time", "data", "dependency-injection", "events", "grpc", "http", "inheritance", "structural"];

    private static string? Detail(RelationFactJson relation, string key) =>
        relation.Details?.FirstOrDefault(detail => detail.Key == key)?.Value;
}

public sealed class RelationResolverEndToEndFixture : IAsyncLifetime
{
    private static readonly string[] Projects =
        [SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts];

    public string Output { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-relr-e2e-{Guid.NewGuid():N}");

    public string OutputB { get; } = Path.Combine(Path.GetTempPath(), $"csharp2md-relr-e2e-{Guid.NewGuid():N}");

    public ImmutableArray<RelationFactJson> StructuralRelations { get; private set; }

    public ImmutableArray<RelationFactJson> DataRelations { get; private set; }

    public IReadOnlyDictionary<string, ImmutableArray<RelationFactJson>> RelationsByPartition { get; private set; } = null!;

    internal ResolutionMetricsAggregate Resolution { get; private set; } = null!;

    public string Diagnostics { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await RunAsync(Output);
        await RunAsync(OutputB);

        var raw = TopicLayout.RawRoot(Output);
        var wireNames = new[] { "compile-time", "data", "dependency-injection", "events", "grpc", "http", "inheritance", "structural" };
        var byPartition = new Dictionary<string, ImmutableArray<RelationFactJson>>(StringComparer.Ordinal);
        foreach (var wireName in wireNames)
        {
            byPartition[wireName] = Deserialize(
                Path.Combine(raw, "facts", "relations", $"{wireName}.json"),
                AggregateJsonContext.Default.RelationAggregate).Entries;
        }

        RelationsByPartition = byPartition;
        StructuralRelations = byPartition["structural"];
        DataRelations = byPartition["data"];
        Resolution = Deserialize(
            Path.Combine(raw, "facts", "relations", "resolution.json"), AggregateJsonContext.Default.ResolutionMetricsAggregate);
        Diagnostics = File.ReadAllText(Path.Combine(raw, "facts", "diagnostics.json"));
    }

    public Task DisposeAsync()
    {
        foreach (var output in new[] { Output, OutputB })
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }

        return Task.CompletedTask;
    }

    private static async Task RunAsync(string output)
    {
        var manifestDirectory = Directory.CreateTempSubdirectory("csharp2md-relr-e2e-manifest-").FullName;
        var manifest = FixtureManifest.WriteOverrides(manifestDirectory, Projects);

        var request = Assert.IsType<AnalysisRequest>(
            AnalysisRequest.Create(manifest, output, topic: "acme-relation-resolver-e2e", domain: "system-design").Request);
        var result = await new AnalysisEngine().AnalyzeAsync(request);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"fixture analysis failed with exit {result.ExitCode}: {string.Join(" | ", result.Diagnostics)}");
        }
    }

    private static T Deserialize<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo) =>
        JsonSerializer.Deserialize(File.ReadAllBytes(path), typeInfo)
            ?? throw new InvalidOperationException($"{path} deserialized to null.");
}
