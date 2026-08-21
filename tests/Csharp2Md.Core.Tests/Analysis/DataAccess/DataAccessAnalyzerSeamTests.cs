using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class DataAccessAnalyzerSeamTests
{
    // The seam is usable: a type outside the collector implements it and contributes a claim.
    [Fact]
    public void Analyze_ImplementationOfTheSeam_AppendsItsClaimToTheBuilder()
    {
        var context = DataAccessTestFacts.Context();
        var claims = ImmutableArray.CreateBuilder<RawDatabaseClaim>();
        IDataAccessAnalyzer analyzer = StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.stub", "tb_order");

        analyzer.Analyze(context, claims);

        var claim = Assert.Single(claims);
        Assert.Equal("tb_order", claim.ObjectText);
        Assert.Equal(DatabaseClaimKind.Access, claim.Kind);
        Assert.Equal(analyzer.Id, claim.AnalyzerId);
        Assert.Equal(context.DocumentId, claim.Evidence.DocumentId);
    }

    // The identity member of the seam is the analyzer's detector provenance.
    [Fact]
    public void Id_OfAnImplementation_IsTheReverseDnsDetectorIdentity()
    {
        IDataAccessAnalyzer analyzer = StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.stub", "tb_order");

        Assert.Equal(
            DetectorId.Create("csharp2md.dataaccess.stub").Value,
            analyzer.Id.ToDetectorId().Value);
    }

    // Analyzers append; they never rewrite what an earlier analyzer contributed.
    [Fact]
    public void Analyze_OverABuilderThatAlreadyHasClaims_LeavesTheEarlierClaimsIntact()
    {
        var context = DataAccessTestFacts.Context();
        var claims = ImmutableArray.CreateBuilder<RawDatabaseClaim>();
        var first = StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.first", "tb_first");
        var second = StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.second", "tb_second");

        first.Analyze(context, claims);
        second.Analyze(context, claims);

        Assert.Equal(["tb_first", "tb_second"], claims.Select(static claim => claim.ObjectText));
    }
}
