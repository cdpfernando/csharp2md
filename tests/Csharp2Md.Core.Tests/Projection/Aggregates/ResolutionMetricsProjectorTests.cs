using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class ResolutionMetricsProjectorTests
{
    [Fact]
    public void Project_ZeroRelationProjection_YieldsEveryCountAtZeroNotAnEmptyObject()
    {
        var result = new RelationProjectionResult(
            [.. Enum.GetValues<RelationPartition>().Select(static partition => new RelationPartitionProjection(partition, []))]);

        var aggregate = ResolutionMetricsProjector.Project(result);

        Assert.Equal(0, aggregate.Total);
        AssertAllZero(aggregate.ByMethod);
        Assert.Equal(Enum.GetValues<RelationPartition>().Length, aggregate.ByPartition.Length);
        Assert.All(aggregate.ByPartition, partition =>
        {
            Assert.Equal(0, partition.Total);
            AssertAllZero(partition.ByMethod);
        });
    }

    [Fact]
    public void Project_AllEightMethodKeys_AreAlwaysPresentAtZeroWhenUnused()
    {
        var result = SingleRelationProjection(RelationPartition.Structural, "syntactic");

        var aggregate = ResolutionMetricsProjector.Project(result);

        Assert.Equal(1, aggregate.ByMethod.Syntactic);
        Assert.Equal(0, aggregate.ByMethod.Exact);
        Assert.Equal(0, aggregate.ByMethod.Candidate);
        Assert.Equal(0, aggregate.ByMethod.Configured);
        Assert.Equal(0, aggregate.ByMethod.Convention);
        Assert.Equal(0, aggregate.ByMethod.Dynamic);
        Assert.Equal(0, aggregate.ByMethod.Heuristic);
        Assert.Equal(0, aggregate.ByMethod.Unresolved);
    }

    [Fact]
    public void Project_PerPartitionBreakdown_CoversEveryRelationPartitionMemberProjectedOverTheEnum()
    {
        var result = new RelationProjectionResult(
            [.. Enum.GetValues<RelationPartition>().Select(static partition => new RelationPartitionProjection(partition, []))]);

        var aggregate = ResolutionMetricsProjector.Project(result);

        var expectedNames = Enum.GetValues<RelationPartition>()
            .Select(FactualJsonMapper.WireRelationPartition)
            .Order(StringComparer.Ordinal);
        Assert.Equal(expectedNames, aggregate.ByPartition.Select(static entry => entry.Partition));
    }

    [Fact]
    public void Project_Totals_EqualTheRelationsInTheProjectedPartitions()
    {
        var partitions = new[]
        {
            new RelationPartitionProjection(RelationPartition.Structural, [Relation("syntactic"), Relation("unresolved")]),
            new RelationPartitionProjection(RelationPartition.Data, [Relation("configured")]),
        }.Concat(Enum.GetValues<RelationPartition>()
            .Where(static partition => partition is not (RelationPartition.Structural or RelationPartition.Data))
            .Select(static partition => new RelationPartitionProjection(partition, [])))
            .ToImmutableArray();
        var result = new RelationProjectionResult(partitions);

        var aggregate = ResolutionMetricsProjector.Project(result);

        // Asserted rather than assumed: recompute the expected total directly from the same partitions
        // input, independently of the projector's own internal arithmetic.
        var expectedTotal = partitions.Sum(static partition => partition.Relations.Length);
        Assert.Equal(3, expectedTotal);
        Assert.Equal(expectedTotal, aggregate.Total);
        Assert.Equal(
            expectedTotal,
            aggregate.ByPartition.Sum(static entry => entry.Total));
        var structural = Assert.Single(aggregate.ByPartition, static entry => entry.Partition == "structural");
        Assert.Equal(2, structural.Total);
        var data = Assert.Single(aggregate.ByPartition, static entry => entry.Partition == "data");
        Assert.Equal(1, data.Total);
    }

    private static void AssertAllZero(ResolutionMethodCountsJson counts)
    {
        Assert.Equal(0, counts.Exact);
        Assert.Equal(0, counts.Candidate);
        Assert.Equal(0, counts.Syntactic);
        Assert.Equal(0, counts.Configured);
        Assert.Equal(0, counts.Convention);
        Assert.Equal(0, counts.Dynamic);
        Assert.Equal(0, counts.Heuristic);
        Assert.Equal(0, counts.Unresolved);
    }

    private static RelationProjectionResult SingleRelationProjection(RelationPartition partition, string method) =>
        new(
            [.. Enum.GetValues<RelationPartition>().Select(candidate =>
                new RelationPartitionProjection(candidate, candidate == partition ? [Relation(method)] : []))]);

    private static RelationFactJson Relation(string resolutionMethod) => new(
        new FactHeaderJson("id1:relation;owner=x;kind=calls;claim=y;ordinal=1", "relation", "syntactic", [], [], []),
        "id1:relation;owner=x;kind=calls;claim=y;ordinal=1",
        "id1:syntactic-symbol;owner=x",
        null,
        "structural",
        "calls",
        resolutionMethod == "unresolved" ? "No candidate found." : null,
        null,
        resolutionMethod,
        null);
}
