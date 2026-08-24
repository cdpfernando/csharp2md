using Csharp2Md.Core.Analysis.DataAccess;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Relations.Resolution;

public sealed class ExistingTargetStrategyTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();
    private static readonly FactId TargetId =
        DatabaseObjectFactId.Create("unknown", DatabaseObjectKind.Table, "orders").ToFactId();
    private static readonly ExistingTargetStrategy Strategy = new();

    [Fact]
    public void TryResolve_PresentTarget_SurvivesUnchangedWithTheProducersReportedMethod()
    {
        var known = new HashSet<FactId> { OwnerId, TargetId };
        var claim = Claim(TargetId, ResolutionMethod.Configured);

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, EmptyIndex(), known));

        Assert.True(outcome.Handled);
        Assert.Equal(TargetId, outcome.TargetId);
        Assert.Equal(ResolutionMethod.Configured, outcome.Method);
        Assert.Null(outcome.Diagnostic);
    }

    [Fact]
    public void TryResolve_AbsentTarget_NullsMarksUnresolvedAndDiagnosesC2MRELR007AtWarning()
    {
        var known = new HashSet<FactId> { OwnerId };
        var claim = Claim(TargetId, ResolutionMethod.Configured);

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, EmptyIndex(), known));

        Assert.True(outcome.Handled);
        Assert.Null(outcome.TargetId);
        Assert.Equal(ResolutionMethod.Unresolved, outcome.Method);
        Assert.NotNull(outcome.Diagnostic);
        Assert.Equal("C2M-RELR-007", outcome.Diagnostic!.Code);
        Assert.Equal(DiagnosticSeverity.Warning, outcome.Diagnostic.Severity);
    }

    [Fact]
    public void TryResolve_MissingSourceFactId_IsDiagnosedAndStillEmitted()
    {
        var known = new HashSet<FactId> { TargetId };
        var claim = Claim(TargetId, ResolutionMethod.Configured);

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, EmptyIndex(), known));

        Assert.True(outcome.Handled);
        Assert.Equal(TargetId, outcome.TargetId);
        Assert.Equal("C2M-RELR-007", outcome.Diagnostic!.Code);
    }

    [Fact]
    public void TryResolve_NoTargetAtAll_IsDeclinedNotHandled()
    {
        var known = new HashSet<FactId> { OwnerId };
        var claim = Claim(targetId: null, producerMethod: null);

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, EmptyIndex(), known));

        Assert.False(outcome.Handled);
    }

    private static ISymbolIndex EmptyIndex() => SymbolIndexBuilder.Build([], [], [], []);

    private static RawRelation Claim(FactId? targetId, ResolutionMethod? producerMethod) => new()
    {
        Kind = "maps-to",
        OwnerId = OwnerId,
        Evidence = new Evidence(DocumentId, "Worker.cs", 1, 1, 1, 10),
        ShapeConfidence = FactResolution.Exact,
        Partition = RelationPartition.Data,
        Details = [new RelationDetail("target_text", "orders")],
        TargetText = "orders",
        TargetId = targetId,
        ProducerMethod = producerMethod,
    };
}
