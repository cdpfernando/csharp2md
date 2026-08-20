using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Facts.Metadata;

namespace Csharp2Md.Core.Tests.Analysis.DataAccess;

public sealed class DataAccessCollectorTests
{
    // Spec Edge Case: a document with no persistence API usage is silent - no claims, and no
    // diagnostic either. Runs the real registered analyzers rather than a stub.
    [Fact]
    public void Collect_DocumentWithNoPersistenceApiUsage_YieldsNoClaimsAndNoDiagnostics()
    {
        var collection = DataAccessCollector.Collect(DataAccessTestFacts.Context(
            """
            class OrderRepository
            {
                public string Describe(Order order) => order.ToString();
            }
            """));

        Assert.Empty(collection.Claims);
        Assert.Empty(collection.Diagnostics);
    }

    // DAD-18: the failure is recorded against the document and the analyzer, and nothing escapes.
    [Fact]
    public void Collect_WhenAnAnalyzerThrows_RecordsOneC2MDA001WarningNamingTheDocumentAndAnalyzer()
    {
        var context = DataAccessTestFacts.Context();
        var failing = StubDataAccessAnalyzer.AppendingThenThrowing(
            "csharp2md.dataaccess.failing", new InvalidOperationException("boom"));

        var collection = DataAccessCollector.Collect(context, [failing]);

        var diagnostic = Assert.Single(collection.Diagnostics);
        Assert.Equal("C2M-DA-001", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(context.DocumentId.ToFactId(), diagnostic.ScopeId);
        Assert.Contains(
            new DiagnosticData("analyzer_id", failing.Id.Value), diagnostic.Data);
        Assert.Contains(
            new DiagnosticData("document_path", "src/App/OrderRepository.cs"), diagnostic.Data);
        Assert.Contains(
            new DiagnosticData("exception_type", typeof(InvalidOperationException).FullName!), diagnostic.Data);
        Assert.Equal(failing.Id.ToDetectorId(), diagnostic.ExtensionId);
    }

    // DAD-18: partial work from a failed analyzer is discarded rather than half-reported.
    [Fact]
    public void Collect_WhenAnAnalyzerThrows_DiscardsThatAnalyzersPartialClaims()
    {
        var context = DataAccessTestFacts.Context();
        var failing = StubDataAccessAnalyzer.AppendingThenThrowing(
            "csharp2md.dataaccess.failing", new InvalidOperationException("boom"));

        var collection = DataAccessCollector.Collect(context, [failing]);

        Assert.Empty(collection.Claims);
    }

    // DAD-18: the run continues - one analyzer's failure does not cost another analyzer's claims.
    [Fact]
    public void Collect_WhenOneAnalyzerThrows_StillReturnsTheOtherAnalyzersClaims()
    {
        var context = DataAccessTestFacts.Context();
        var failing = StubDataAccessAnalyzer.AppendingThenThrowing(
            "csharp2md.dataaccess.failing", new InvalidOperationException("boom"));
        var healthy = StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.healthy", "tb_order");

        var collection = DataAccessCollector.Collect(context, [failing, healthy]);

        var claim = Assert.Single(collection.Claims);
        Assert.Equal("tb_order", claim.ObjectText);
        Assert.Equal(healthy.Id, claim.AnalyzerId);
        Assert.Single(collection.Diagnostics);
        Assert.Equal(1, healthy.AnalyzeCount);
    }

    // Cancellation is not an analyzer defect, so it propagates rather than becoming a diagnostic -
    // the same rule DetectorHost applies.
    [Fact]
    public void Collect_WhenAnAnalyzerIsCancelled_PropagatesInsteadOfRecordingADiagnostic()
    {
        var context = DataAccessTestFacts.Context();
        var cancelled = StubDataAccessAnalyzer.AppendingThenThrowing(
            "csharp2md.dataaccess.cancelled", new OperationCanceledException());

        Assert.Throws<OperationCanceledException>(
            () => DataAccessCollector.Collect(context, [cancelled]));
    }

    // DAD-13: evidence is a required member of the claim, so nothing evidence-free can reach the
    // collector's output. The construction-time rejection is pinned in
    // DataAccessContractsTests.RawDatabaseClaim_WithoutEvidence_IsRejectedAtConstruction.
    [Fact]
    public void Collect_EveryReturnedClaim_CarriesEvidenceForItsOwnDocument()
    {
        var context = DataAccessTestFacts.Context();
        var analyzers = new[]
        {
            StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.first", "tb_first"),
            StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.second", "tb_second"),
        };

        var collection = DataAccessCollector.Collect(context, analyzers);

        Assert.Equal(2, collection.Claims.Length);
        Assert.All(collection.Claims, claim =>
        {
            Assert.NotEqual(default, claim.Evidence);
            Assert.Equal(context.DocumentId, claim.Evidence.DocumentId);
            Assert.Equal("src/App/OrderRepository.cs", claim.Evidence.RelativePath);
            Assert.True(claim.Evidence.StartLine >= 1);
            Assert.True(claim.Evidence.StartColumn >= 1);
        });
    }

    // DAD-20: registration order must not reach the output, and two collections over the same input
    // must agree.
    [Fact]
    public void Collect_AnalyzerOrder_IsCanonicalAndStableAcrossTwoCollections()
    {
        var context = DataAccessTestFacts.Context();
        var alpha = StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.alpha", "tb_alpha");
        var omega = StubDataAccessAnalyzer.Appending("csharp2md.dataaccess.omega", "tb_omega");

        var registeredForwards = DataAccessCollector.Collect(context, [alpha, omega]);
        var registeredBackwards = DataAccessCollector.Collect(context, [omega, alpha]);

        Assert.Equal(
            ["tb_alpha", "tb_omega"],
            registeredForwards.Claims.Select(static claim => claim.ObjectText));
        Assert.Equal(
            registeredForwards.Claims.Select(static claim => claim.ObjectText),
            registeredBackwards.Claims.Select(static claim => claim.ObjectText));
    }

    // Phase 2 ships the seam with nothing behind it; the analyzers arrive in Phase 3 and Phase 4.
    [Fact]
    public void Collect_WithTheRegisteredAnalyzers_ProducesNothingUntilAnAnalyzerExists()
    {
        var collection = DataAccessCollector.Collect(DataAccessTestFacts.Context());

        Assert.Empty(collection.Claims);
        Assert.Empty(collection.Diagnostics);
    }
}
