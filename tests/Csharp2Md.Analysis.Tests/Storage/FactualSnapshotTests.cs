using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class FactualSnapshotTests
{
    [Fact]
    [Trait("Requirement", "STOR-10")]
    public void Empty_HasLengthZeroArraysForEveryFamily()
    {
        var empty = FactualSnapshot.Empty;

        Assert.False(empty.Facts.IsDefault);
        Assert.Empty(empty.Facts);
        Assert.False(empty.Observations.IsDefault);
        Assert.Empty(empty.Observations);
        Assert.False(empty.ConfirmedRelations.IsDefault);
        Assert.Empty(empty.ConfirmedRelations);
        Assert.False(empty.Candidates.IsDefault);
        Assert.Empty(empty.Candidates);
        Assert.False(empty.Unresolved.IsDefault);
        Assert.Empty(empty.Unresolved);
        Assert.False(empty.Frontiers.IsDefault);
        Assert.Empty(empty.Frontiers);
    }

    [Fact]
    [Trait("Requirement", "STOR-10")]
    public void Merge_SharedSolutionIdentity_DoesNotThrowAndConcatenatesFacts()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = Solution.Create(SolutionId.Create(workspace, "src/Acme.sln"));

        var left = SnapshotWithFact(solution);
        var right = SnapshotWithFact(solution);

        var merged = left.Merge(right);

        Assert.Equal(2, merged.Facts.Length);
        Assert.Equal(solution, merged.Facts[0]);
        Assert.Equal(solution, merged.Facts[1]);
        Assert.Empty(merged.Observations);
        Assert.Empty(merged.ConfirmedRelations);
        Assert.Empty(merged.Candidates);
        Assert.Empty(merged.Unresolved);
        Assert.Empty(merged.Frontiers);
    }

    private static FactualSnapshot SnapshotWithFact(IFact fact) =>
        new(
            ImmutableArray.Create(fact),
            ImmutableArray<Observation>.Empty,
            ImmutableArray<ConfirmedRelation>.Empty,
            ImmutableArray<CandidateLink>.Empty,
            ImmutableArray<UnresolvedRecord>.Empty,
            ImmutableArray<OpenFrontier>.Empty);
}
