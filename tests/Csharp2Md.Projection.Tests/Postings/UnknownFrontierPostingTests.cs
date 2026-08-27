using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Postings;

public sealed class UnknownFrontierPostingTests
{
    private static readonly string[] ConfirmedPostingKeys =
    [
        PostingProjector.OutgoingKey,
        PostingProjector.IncomingKey,
        PostingProjector.CallersKey,
        PostingProjector.CalleesKey,
        PostingProjector.ContractProducersKey,
        PostingProjector.ContractConsumersKey,
        PostingProjector.DataReadersKey,
        PostingProjector.DataWritersKey,
    ];

    [Fact]
    [Trait("Requirement", "RP-29")]
    public void Project_UnresolvedOwner_DoesNotAppearInConfirmedPostings()
    {
        var (view, unresolvedOwner, _, _) = SeparationFixture();

        Assert.DoesNotContain(ConfirmedFactIds(view), id => id == unresolvedOwner);
        Assert.Contains(view.Document.Unresolved, record => record.Source.Id == unresolvedOwner);
    }

    [Fact]
    [Trait("Requirement", "RP-29")]
    public void Project_CandidateSource_DoesNotAppearInConfirmedPostings()
    {
        var (view, _, candidateSource, _) = SeparationFixture();

        Assert.DoesNotContain(ConfirmedFactIds(view), id => id == candidateSource);
        Assert.Contains(view.Document.Candidates, link => link.Source.Id == candidateSource);
    }

    [Fact]
    [Trait("Requirement", "RP-29")]
    public void Project_FrontierOwner_DoesNotAppearInConfirmedPostings()
    {
        var (view, _, _, frontierOwner) = SeparationFixture();

        Assert.DoesNotContain(ConfirmedFactIds(view), id => id == frontierOwner);
        Assert.Contains(view.Document.Frontiers, frontier => frontier.Occurrence.Owner.Id == frontierOwner);
    }

    [Fact]
    [Trait("Requirement", "RP-29")]
    public void Project_ConfirmedPostingEntries_DoNotCiteCandidateUnresolvedOrFrontierArtifacts()
    {
        var (view, _, _, _) = SeparationFixture();

        foreach (var entry in ConfirmedEntries(view))
        {
            Assert.StartsWith("relations/confirmed/", entry.ArtifactKey, StringComparison.Ordinal);
            Assert.DoesNotContain("candidates", entry.ArtifactKey, StringComparison.Ordinal);
            Assert.DoesNotContain("unresolved", entry.ArtifactKey, StringComparison.Ordinal);
            Assert.DoesNotContain("frontiers", entry.ArtifactKey, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-29")]
    public void Project_Frontiers_CiteFrontiersJson()
    {
        var (view, _, _, frontierOwner) = SeparationFixture();

        var group = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.FrontiersKey),
            candidate => candidate.FactId == frontierOwner);
        var entry = Assert.Single(group.Entries);
        Assert.Equal("relations/frontiers.json", entry.ArtifactKey);
        Assert.True(entry.Ordinal >= 0);
    }

    [Fact]
    [Trait("Requirement", "RP-29")]
    public void Project_Frontiers_CitedOrdinalResolvesToThatFrontier()
    {
        var (view, _, _, frontierOwner) = SeparationFixture();

        var group = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.FrontiersKey),
            candidate => candidate.FactId == frontierOwner);
        var entry = Assert.Single(group.Entries);
        Assert.InRange(entry.Ordinal, 0, view.Document.Frontiers.Length - 1);
        var cited = view.Document.Frontiers[entry.Ordinal];
        Assert.Equal(frontierOwner, cited.Occurrence.Owner.Id);
        Assert.Equal(group.FactId, cited.Occurrence.Owner.Id);
    }

    [Fact]
    [Trait("Requirement", "RP-29")]
    public void Project_Unknowns_CiteUnresolvedJsonAtResolvingOrdinal()
    {
        var (view, unresolvedOwner, _, _) = SeparationFixture();

        var group = Assert.Single(
            PostingProjectionFactory.ReadPosting(PostingProjector.Project(view), PostingProjector.UnknownsKey),
            candidate => candidate.FactId == unresolvedOwner);
        var entry = Assert.Single(group.Entries);
        Assert.Equal("relations/unresolved.json", entry.ArtifactKey);
        Assert.InRange(entry.Ordinal, 0, view.Document.Unresolved.Length - 1);
        Assert.Equal(unresolvedOwner, view.Document.Unresolved[entry.Ordinal].Source.Id);
        PostingProjectionFactory.AssertCitationOnly(entry);
    }

    private static HashSet<string> ConfirmedFactIds(PublishedPackageView view)
    {
        var fragments = PostingProjector.Project(view);
        return fragments
            .Where(fragment => ConfirmedPostingKeys.Contains(fragment.CanonicalKey, StringComparer.Ordinal))
            .SelectMany(fragment => PostingProjectionFactory.ReadPosting([fragment], fragment.CanonicalKey))
            .Select(static group => group.FactId)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static IEnumerable<Csharp2Md.Storage.Wire.PostingEntryDto> ConfirmedEntries(PublishedPackageView view)
    {
        var fragments = PostingProjector.Project(view);
        return fragments
            .Where(fragment => ConfirmedPostingKeys.Contains(fragment.CanonicalKey, StringComparer.Ordinal))
            .SelectMany(fragment => PostingProjectionFactory.ReadPosting([fragment], fragment.CanonicalKey))
            .SelectMany(static group => group.Entries);
    }

    private static (
        PublishedPackageView View,
        string UnresolvedOwner,
        string CandidateSource,
        string FrontierOwner) SeparationFixture()
    {
        var solution = CatalogProjectionFactory.CreateSolutionFact();
        var project = CatalogProjectionFactory.CreateProjectFact("src/Acme.Orders/Acme.Orders.csproj");
        var unresolvedOwner = CatalogProjectionFactory.CreateProjectFact("src/Acme.Zero/Zero.csproj");
        var candidateSource = CatalogProjectionFactory.Callable("SpeculateAsync");
        var callee = CatalogProjectionFactory.Callable("Authorize");
        var frontierOwner = CatalogProjectionFactory.Callable("ContinueAsync");
        var view = CatalogProjectionFactory.ViewOf(
            [solution, project, unresolvedOwner, candidateSource, callee, frontierOwner],
            [CatalogProjectionFactory.Contains(solution.Reference, project.Reference)],
            unresolved: [CatalogProjectionFactory.CreateUnresolved(RelationKind.Invokes, unresolvedOwner.Reference)],
            candidates:
            [
                CandidateLink.Create(
                    RelationKind.Invokes,
                    candidateSource.Reference,
                    callee.Reference,
                    PostingProjectionFactory.Evidence(candidateSource.Reference, 2)),
            ],
            frontiers:
            [
                OpenFrontier.Create(
                    new ObservationIdentity(
                        frontierOwner.Reference,
                        ObservationKind.Invocation,
                        NormalizedPayload.Create([]),
                        1),
                    FrontierCause.FurtherContinuationObserved),
            ]);
        return (view, unresolvedOwner.Reference.Id.Value, candidateSource.Reference.Id.Value, frontierOwner.Reference.Id.Value);
    }
}
