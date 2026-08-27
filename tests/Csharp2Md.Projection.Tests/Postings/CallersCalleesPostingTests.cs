using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Postings;

public sealed class CallersCalleesPostingTests
{
    [Fact]
    [Trait("Requirement", "RP-28")]
    public void Project_Callers_OnlyConfirmedInvokesContribute()
    {
        var (view, _, callee, _, _) = InvokesFixture();

        var callers = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.CallersKey),
            group => group.FactId == callee.Reference.Id.Value);

        Assert.All(callers.Entries, entry =>
        {
            Assert.Equal("relations/confirmed/invokes.json", entry.ArtifactKey);
            Assert.Equal("invokes", PostingProjectionFactory.RelationAt(view, entry).Kind);
        });
        Assert.DoesNotContain(
            callers.Entries,
            entry => entry.ArtifactKey.Contains("contains", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-28")]
    public void Project_Callees_OnlyConfirmedInvokesContribute()
    {
        var (view, firstCaller, _, _, _) = InvokesFixture();

        var callees = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.CalleesKey),
            group => group.FactId == firstCaller.Reference.Id.Value);

        Assert.All(callees.Entries, entry =>
        {
            Assert.Equal("relations/confirmed/invokes.json", entry.ArtifactKey);
            Assert.Equal("invokes", PostingProjectionFactory.RelationAt(view, entry).Kind);
        });
    }

    [Fact]
    [Trait("Requirement", "RP-28")]
    [Trait("Requirement", "RP-29")]
    public void Project_CandidateInvocation_AppearsInNeitherCallersNorCallees()
    {
        var (view, _, callee, candidateCaller, _) = InvokesFixture();

        var fragments = PostingProjector.Project(view);
        var callers = PostingProjectionFactory.ReadPosting(fragments, PostingProjector.CallersKey);
        var callees = PostingProjectionFactory.ReadPosting(fragments, PostingProjector.CalleesKey);
        var candidateId = candidateCaller.Reference.Id.Value;

        Assert.Contains(view.Document.Candidates, link => link.Kind == "invokes" && link.Source.Id == candidateId);
        Assert.DoesNotContain(
            view.Document.ConfirmedRelations["invokes"],
            relation => relation.Source.Id == candidateId);
        Assert.DoesNotContain(callers, group => group.FactId == candidateId);
        Assert.DoesNotContain(callees, group => group.FactId == candidateId);
        var calleeCallers = Assert.Single(callers, group => group.FactId == callee.Reference.Id.Value);
        Assert.DoesNotContain(
            calleeCallers.Entries,
            entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id == candidateId);
    }

    [Fact]
    [Trait("Requirement", "RP-28")]
    public void Project_Callers_ForKnownCallee_ListsExactlyTheCallersInConfirmedInvokes()
    {
        var (view, firstCaller, callee, _, secondCaller) = InvokesFixture();
        var expected = view.Document.ConfirmedRelations["invokes"]
            .Where(relation => relation.Target.Id == callee.Reference.Id.Value)
            .Select(relation => relation.Source.Id)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        var callers = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.CallersKey),
            group => group.FactId == callee.Reference.Id.Value);
        var actual = callers.Entries
            .Select(entry => PostingProjectionFactory.RelationAt(view, entry).Source.Id)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.Equal(2, actual.Length);
        Assert.Contains(firstCaller.Reference.Id.Value, actual);
        Assert.Contains(secondCaller.Reference.Id.Value, actual);
    }

    [Fact]
    [Trait("Requirement", "RP-28")]
    public void Project_Callees_ForKnownCaller_ListsExactlyTheCalleesInConfirmedInvokes()
    {
        var (view, firstCaller, callee, _, _) = InvokesFixture();
        var expected = view.Document.ConfirmedRelations["invokes"]
            .Where(relation => relation.Source.Id == firstCaller.Reference.Id.Value)
            .Select(relation => relation.Target.Id)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        var callees = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.CalleesKey),
            group => group.FactId == firstCaller.Reference.Id.Value);
        var actual = callees.Entries
            .Select(entry => PostingProjectionFactory.RelationAt(view, entry).Target.Id)
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, actual);
        Assert.Equal(callee.Reference.Id.Value, Assert.Single(actual));
    }

    [Fact]
    [Trait("Requirement", "RP-26")]
    [Trait("Requirement", "RP-28")]
    public void Project_CallersAndCallees_EntriesCarryKeyAndOrdinalOnly()
    {
        var (view, _, _, _, _) = InvokesFixture();

        var fragments = PostingProjector.Project(view);
        PostingProjectionFactory.AssertEntriesAreCitationsOnly(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.CallersKey));
        PostingProjectionFactory.AssertEntriesAreCitationsOnly(
            PostingProjectionFactory.ReadPosting(fragments, PostingProjector.CalleesKey));
    }

    [Fact]
    [Trait("Requirement", "RP-28")]
    public void Project_Callers_CitedOrdinalResolvesToTheClaimedInvokesRelation()
    {
        var (view, _, callee, _, _) = InvokesFixture();

        var callers = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.CallersKey),
            group => group.FactId == callee.Reference.Id.Value);

        foreach (var entry in callers.Entries)
        {
            var relation = PostingProjectionFactory.RelationAt(view, entry);
            Assert.Equal("invokes", relation.Kind);
            Assert.Equal(callee.Reference.Id.Value, relation.Target.Id);
            Assert.Equal(callers.FactId, relation.Target.Id);
        }
    }

    private static (
        PublishedPackageView View,
        Domain.Facts.Symbol FirstCaller,
        Domain.Facts.Symbol Callee,
        Domain.Facts.Symbol CandidateCaller,
        Domain.Facts.Symbol SecondCaller) InvokesFixture()
    {
        var solution = CatalogProjectionFactory.CreateSolutionFact();
        var project = CatalogProjectionFactory.CreateProjectFact("src/Acme.Orders/Acme.Orders.csproj");
        var firstCaller = CatalogProjectionFactory.Callable("PlaceAsync");
        var secondCaller = CatalogProjectionFactory.Callable("RetryAsync");
        var callee = CatalogProjectionFactory.Callable("Authorize");
        var candidateCaller = CatalogProjectionFactory.Callable("SpeculateAsync");
        var view = CatalogProjectionFactory.ViewOf(
            [solution, project, firstCaller, secondCaller, callee, candidateCaller],
            [
                CatalogProjectionFactory.Contains(solution.Reference, project.Reference),
                PostingProjectionFactory.Invokes(firstCaller, callee, 1),
                PostingProjectionFactory.Invokes(secondCaller, callee, 2),
            ],
            candidates:
            [
                CandidateLink.Create(
                    RelationKind.Invokes,
                    candidateCaller.Reference,
                    callee.Reference,
                    PostingProjectionFactory.Evidence(candidateCaller.Reference, 3)),
            ]);
        return (view, firstCaller, callee, candidateCaller, secondCaller);
    }
}
