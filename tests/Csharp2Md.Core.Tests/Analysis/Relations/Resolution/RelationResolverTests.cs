using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Relations.Resolution;

public sealed class RelationResolverTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();
    private static readonly ISymbolIndex EmptyIndex = SymbolIndexBuilder.Build([], [], [], []);
    private static readonly HashSet<FactId> NoKnownFacts = [];

    [Fact]
    public void Resolve_FirstStrategyHandles_StopsBeforeTheSecondStrategyRuns()
    {
        var first = RecordingStrategy.Handling(ResolutionMethod.Syntactic);
        var second = RecordingStrategy.Handling(ResolutionMethod.Unresolved);
        var resolver = new RelationResolver([first, second]);
        var snapshot = SnapshotOf(Claim("calls", "Foo"));

        resolver.Resolve(snapshot, EmptyIndex, NoKnownFacts, CancellationToken.None);

        Assert.Equal(1, first.CallCount);
        Assert.Equal(0, second.CallCount);
    }

    [Fact]
    public void Resolve_AStrategyThrows_DiscardsItsOutcomeAndContinuesToTheNextStrategy()
    {
        var throwing = RecordingStrategy.Throwing(new InvalidOperationException("boom"));
        var next = RecordingStrategy.Handling(ResolutionMethod.Syntactic);
        var resolver = new RelationResolver([throwing, next]);
        var snapshot = SnapshotOf(Claim("calls", "Foo"));

        var resolution = resolver.Resolve(snapshot, EmptyIndex, NoKnownFacts, CancellationToken.None);

        Assert.Equal(1, throwing.CallCount);
        Assert.Equal(1, next.CallCount);
        var fact = Assert.Single(resolution.Facts);
        Assert.Equal(ResolutionMethod.Syntactic, fact.Method);
        var diagnostic = Assert.Single(resolution.Diagnostics);
        Assert.Equal("C2M-RELR-006", diagnostic.Code);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Equal(fact.Header.Id, diagnostic.ScopeId);
    }

    [Fact]
    public void Resolve_EveryClaim_YieldsExactlyOneRelation()
    {
        var resolver = RelationResolver.Default;
        var snapshot = SnapshotOf(Claim("calls", "Foo"), Claim("creates", "Bar"), Claim("references", "Baz"));

        var resolution = resolver.Resolve(snapshot, EmptyIndex, NoKnownFacts, CancellationToken.None);

        Assert.Equal(3, resolution.Facts.Length);
    }

    [Fact]
    public void Resolve_NoStrategyHandles_UnresolvedStrategyNamesTheObservedTextAndDiagnosesC2MRELR001()
    {
        var resolver = RelationResolver.Default;
        var snapshot = SnapshotOf(Claim("calls", "PaymentClient.Authorize"));

        var resolution = resolver.Resolve(snapshot, EmptyIndex, NoKnownFacts, CancellationToken.None);

        var fact = Assert.Single(resolution.Facts);
        Assert.Equal(ResolutionMethod.Unresolved, fact.Method);
        Assert.Null(fact.TargetId);
        Assert.Contains("PaymentClient.Authorize", fact.UnresolvedReason);
        var diagnostic = Assert.Single(resolution.Diagnostics);
        Assert.Equal("C2M-RELR-001", diagnostic.Code);
        Assert.Equal(fact.Header.Id, diagnostic.ScopeId);
    }

    [Fact]
    public void Resolve_CancelledToken_PropagatesAndIsNeverConvertedIntoADiagnostic()
    {
        var resolver = RelationResolver.Default;
        var snapshot = SnapshotOf(Claim("calls", "Foo"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => resolver.Resolve(snapshot, EmptyIndex, NoKnownFacts, cts.Token));
    }

    /// <summary>An <c>IRelationResolutionStrategy</c> whose outcome each test supplies, recording how often it ran.</summary>
    private sealed class RecordingStrategy : IRelationResolutionStrategy
    {
        private readonly Func<RelationResolutionContext, RelationResolutionOutcome> _resolve;

        private RecordingStrategy(Func<RelationResolutionContext, RelationResolutionOutcome> resolve) =>
            _resolve = resolve;

        public int CallCount { get; private set; }

        public RelationResolutionOutcome TryResolve(RelationResolutionContext context)
        {
            CallCount++;
            return _resolve(context);
        }

        public static RecordingStrategy Handling(ResolutionMethod method) =>
            new(_ => new RelationResolutionOutcome(Handled: true, Method: method));

        public static RecordingStrategy Throwing(Exception exception) =>
            new(_ => throw exception);
    }

    private static RelationClaimSnapshot SnapshotOf(params RawRelation[] claims)
    {
        var accumulator = new RelationClaimAccumulator();
        accumulator.Add(DocumentId, "Worker.cs", [40], [.. claims]);
        return accumulator.ToSnapshot();
    }

    private static RawRelation Claim(string kind, string targetText) => new()
    {
        Kind = kind,
        OwnerId = OwnerId,
        Evidence = new Evidence(DocumentId, "Worker.cs", 1, 1, 1, 10),
        ShapeConfidence = FactResolution.Syntactic,
        Partition = RelationPartition.Structural,
        Details = [new RelationDetail("target_text", targetText)],
        TargetText = targetText,
    };
}
