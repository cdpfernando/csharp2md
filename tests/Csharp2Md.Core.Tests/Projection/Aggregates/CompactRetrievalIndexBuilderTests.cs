using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class CompactRetrievalIndexBuilderTests
{
    [Fact]
    public void Build_NormalizesKnownRepeatedAndEvidenceOnlyMetadataIntoDenseDeterministicTables()
    {
        var builder = new CompactRetrievalIndexBuilder();
        builder.AddKnownDocument(Document("document-b", "project-b", "B.cs"));
        builder.AddKnownDocument(Document("document-a", "project-a", "A.cs"));
        builder.AddKnownDocument(Document("document-a", "project-a", "A.cs"));
        builder.AddRelation(Relation(0, "source-a", "target-a", evidence: [Document("document-a", null, "A.cs")]));
        builder.AddRelation(Relation(1, "source-b", "target-b", origin: Origin("z.json"), evidence: [Document("document-c", null, "C.cs")]));

        var result = builder.Build();

        Assert.Equal(["document-a", "document-b", "document-c"], result.Documents.Select(static value => value.DocumentId));
        Assert.Equal(["fragment.json", "z.json"], result.Origins.Select(static value => value.FragmentReference));
        Assert.Equal([0], Assert.Single(result.Postings.Projects, static posting => posting.Key == "project-a").RelationOrdinals.ToArray());
        Assert.DoesNotContain(result.Postings.Projects, static posting => posting.Key == "project-b");
    }

    [Fact]
    public void AddKnownDocument_RejectsConflictingMetadata()
    {
        var builder = new CompactRetrievalIndexBuilder();
        builder.AddKnownDocument(Document("document-a", "project-a", "A.cs"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddKnownDocument(Document("document-a", "project-a", "Other.cs")));

        Assert.Contains("document-a", exception.Message, StringComparison.Ordinal);
        Assert.Contains("A.cs", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Other.cs", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_StoresOnlyKeysAndOrdinalsAcrossFivePostingFamiliesAndOmitsAbsentTargetOnly()
    {
        var builder = new CompactRetrievalIndexBuilder();
        builder.AddKnownDocument(Document("document-a", "project-a", "A.cs"));
        builder.AddRelation(Relation(4, "source-a", null, evidence: [Document("document-a", null, "A.cs")]));

        var postings = builder.Build().Postings;

        Assert.Equal("project-a", Assert.Single(postings.Projects).Key);
        Assert.Equal([4], postings.Projects[0].RelationOrdinals.ToArray());
        Assert.Equal("source-a", Assert.Single(postings.Sources).Key);
        Assert.Equal([4], postings.Sources[0].RelationOrdinals.ToArray());
        Assert.Empty(postings.Targets);
        Assert.Equal("calls", Assert.Single(postings.Kinds).Key);
        Assert.Equal([4], postings.Kinds[0].RelationOrdinals.ToArray());
        Assert.Equal("unresolved", Assert.Single(postings.Resolutions).Key);
        Assert.Equal([4], postings.Resolutions[0].RelationOrdinals.ToArray());
    }

    [Fact]
    public void Build_UnknownGroupsAreUniqueAndOrderedByEntryPointImpactThenOrdinal()
    {
        var builder = new CompactRetrievalIndexBuilder();
        builder.AddRelation(Relation(0, "entry", null, relationKind: "aspnet-entrypoint", resolution: "exact", method: "exact"));
        builder.AddRelation(Relation(1, "entry", null, observed: "A"));
        builder.AddRelation(Relation(2, "busy", "target", observed: "B"));
        builder.AddRelation(Relation(3, "busy", "target-2", observed: "B"));
        builder.AddRelation(Relation(4, "quiet", null, observed: "C"));
        builder.AddRelation(Relation(5, "quiet", null, observed: "D"));

        var groups = builder.Build().UnknownGroups;

        Assert.Equal([1], groups[0].RelationOrdinals.ToArray());
        Assert.True(groups[0].HasProvenEntryPoint);
        Assert.Equal([2, 3], groups[1].RelationOrdinals.ToArray());
        Assert.Equal(2, groups[1].Impact);
        Assert.Equal([4], groups[2].RelationOrdinals.ToArray());
        Assert.Equal([5], groups[3].RelationOrdinals.ToArray());
        Assert.Equal(5, groups.SelectMany(static group => group.RelationOrdinals).Distinct().Count());
    }

    [Fact]
    public void Build_MetricsExposeEveryResolutionAndMethodBucketSeparatelyAndCountDistinctEndpoints()
    {
        var builder = new CompactRetrievalIndexBuilder();
        var resolutions = new[] { "exact", "partial", "syntactic", "heuristic", "candidate", "unresolved", "not-applicable" };
        var methods = new[] { "exact", "candidate", "syntactic", "configured", "convention", "dynamic", "heuristic", "unresolved" };
        for (var index = 0; index < methods.Length; index++)
        {
            builder.AddRelation(Relation(index, $"source-{index % 2}", "target", resolution: resolutions[index % resolutions.Length], method: methods[index]));
        }

        var metrics = builder.Build().Metrics;
        var partition = Assert.Single(metrics.ByPartition);

        Assert.Equal(3, metrics.IndexedEndpointCount);
        Assert.Equal(resolutions, partition.Resolutions.Select(static value => value.Name));
        Assert.Equal([2, 1, 1, 1, 1, 1, 1], partition.Resolutions.Select(static value => value.Count));
        Assert.Equal(methods, partition.ResolutionMethods.Select(static value => value.Name));
        Assert.All(partition.ResolutionMethods, static value => Assert.Equal(1, value.Count));
    }

    [Theory]
    [InlineData(10)]
    [InlineData(10_000)]
    public void Build_PreservesHighCardinalityUniqueKeys(int count)
    {
        var builder = new CompactRetrievalIndexBuilder();
        for (var ordinal = 0; ordinal < count; ordinal++)
        {
            builder.AddRelation(Relation(ordinal, $"source-{ordinal:D5}", $"target-{ordinal:D5}"));
        }

        var postings = builder.Build().Postings;

        Assert.Equal(count, postings.Sources.Length);
        Assert.Equal(count, postings.Targets.Length);
        Assert.Equal([0], postings.Sources[0].RelationOrdinals.ToArray());
        Assert.Equal([count - 1], postings.Sources[^1].RelationOrdinals.ToArray());
    }

    private static CompactRelationObservation Relation(
        int ordinal,
        string source,
        string? target,
        string relationKind = "calls",
        string resolution = "unresolved",
        string method = "unresolved",
        string observed = "Target",
        CompactOriginMetadata? origin = null,
        ImmutableArray<CompactDocumentMetadata> evidence = default) =>
        new(
            ordinal,
            source,
            target,
            "structural",
            relationKind,
            resolution,
            method,
            resolution == "unresolved" ? "missing-target" : null,
            observed,
            origin ?? Origin("fragment.json"),
            evidence.IsDefault ? [] : evidence);

    private static CompactDocumentMetadata Document(string id, string? project, string path) =>
        new(id, project, path, GeneratedOrigin: false);

    private static CompactOriginMetadata Origin(string reference) => new(reference, $"hash-{reference}", "3.0.0");
}
