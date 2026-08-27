using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Catalogs;

public sealed class UnknownRankingTests
{
    [Fact]
    [Trait("Requirement", "RP-23")]
    public void Rank_Degree_CountsRelationsWhereOwnerIsSourceOrTarget()
    {
        var (view, highId, midId, zeroId) = DegreeFixture();

        var ranked = UnknownRanking.Rank(view);

        Assert.Equal([highId, midId, zeroId], ranked.Select(item => item.Record.Source.Id));
    }

    [Fact]
    [Trait("Requirement", "RP-23")]
    public void Rank_OwnerWithZeroRelations_RanksLastNotOmitted()
    {
        var (view, _, _, zeroId) = DegreeFixture();

        var ranked = UnknownRanking.Rank(view);

        Assert.Equal(3, ranked.Length);
        Assert.Equal(zeroId, ranked[^1].Record.Source.Id);
        Assert.Contains(view.Document.Unresolved, record => record.Source.Id == zeroId);
    }

    [Fact]
    [Trait("Requirement", "RP-23")]
    public void Rank_EqualDegrees_BreakByFactIdFromFirstKindOrder()
    {
        var (first, second, alphaId, zetaId) = EqualDegreeViews();

        var ranked = UnknownRanking.Rank(first);

        Assert.NotEqual(
            first.Document.Unresolved.Select(static record => record.Source.Id),
            second.Document.Unresolved.Select(static record => record.Source.Id));
        Assert.Equal([alphaId, zetaId], ranked.Select(item => item.Record.Source.Id));
    }

    [Fact]
    [Trait("Requirement", "RP-23")]
    public void Rank_EqualDegrees_BreakByFactIdFromReversedKindOrder()
    {
        var (first, second, alphaId, zetaId) = EqualDegreeViews();

        var ranked = UnknownRanking.Rank(second);

        Assert.NotEqual(
            first.Document.Unresolved.Select(static record => record.Source.Id),
            second.Document.Unresolved.Select(static record => record.Source.Id));
        Assert.Equal([alphaId, zetaId], ranked.Select(item => item.Record.Source.Id));
    }

    internal static (PublishedPackageView View, string HighId, string MidId, string ZeroId) DegreeFixture()
    {
        const string highPath = "src/Acme.High/High.csproj";
        const string midPath = "src/Acme.Mid/Mid.csproj";
        const string zeroPath = "src/Acme.Zero/Zero.csproj";
        var solution = CatalogProjectionFactory.CreateSolutionFact();
        var high = CatalogProjectionFactory.CreateProjectFact(highPath);
        var mid = CatalogProjectionFactory.CreateProjectFact(midPath);
        var zero = CatalogProjectionFactory.CreateProjectFact(zeroPath);
        var document = CatalogProjectionFactory.CreateDocumentFact(highPath, "src/Acme.High/Code.cs");
        var view = CatalogProjectionFactory.ViewOf(
            [solution, high, mid, zero, document],
            [
                CatalogProjectionFactory.Contains(solution.Reference, high.Reference, 1),
                CatalogProjectionFactory.Contains(high.Reference, document.Reference, 2),
                CatalogProjectionFactory.Contains(solution.Reference, mid.Reference, 3),
            ],
            [
                CatalogProjectionFactory.CreateUnresolved(RelationKind.Invokes, high.Reference),
                CatalogProjectionFactory.CreateUnresolved(RelationKind.Invokes, mid.Reference),
                CatalogProjectionFactory.CreateUnresolved(RelationKind.Invokes, zero.Reference),
            ]);
        return (view, high.Reference.Id.Value, mid.Reference.Id.Value, zero.Reference.Id.Value);
    }

    private static (PublishedPackageView First, PublishedPackageView Second, string AlphaId, string ZetaId) EqualDegreeViews()
    {
        var alpha = CatalogProjectionFactory.CreateProjectFact("src/Acme.Alpha/Alpha.csproj");
        var zeta = CatalogProjectionFactory.CreateProjectFact("src/Acme.Zeta/Zeta.csproj");
        var first = CatalogProjectionFactory.ViewOf(
            [alpha, zeta],
            [],
            [
                CatalogProjectionFactory.CreateUnresolved(RelationKind.Invokes, alpha.Reference),
                CatalogProjectionFactory.CreateUnresolved(RelationKind.UsesContract, zeta.Reference),
            ]);
        var second = CatalogProjectionFactory.ViewOf(
            [alpha, zeta],
            [],
            [
                CatalogProjectionFactory.CreateUnresolved(RelationKind.UsesContract, alpha.Reference),
                CatalogProjectionFactory.CreateUnresolved(RelationKind.Invokes, zeta.Reference),
            ]);
        var ordered = new[] { alpha.Reference.Id.Value, zeta.Reference.Id.Value }
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        return (first, second, ordered[0], ordered[1]);
    }
}
