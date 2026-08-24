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

    /// <summary>
    /// T21's named ordinal risk (design.md's Risks table, `RelationCollector.cs:39`): the pre-refactor
    /// collector minted a fresh <c>kind\0claim</c>-keyed ordinal dictionary once per document, with no
    /// owner in the key. The resolver mints run-wide, keyed <c>owner\0kind\0claim</c> (design.md line
    /// 134). This reconstructs the pre-refactor algorithm verbatim (from `RelationCollector.cs` before
    /// commit `fc96d75`, i.e. before T12) and proves it mints byte-identical ids to today's resolver for
    /// a representative multi-owner, multi-kind, single-document claim set - proven, not argued.
    /// </summary>
    [Fact]
    public void Resolve_RepresentativeDocumentClaims_MintsTheSameIdsThePreRefactorCollectorWouldHave()
    {
        var ownerA = SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "method", "class:Worker/Handle/one").ToFactId();
        var ownerB = SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "method", "class:Worker/Handle/two").ToFactId();
        var claims = new[]
        {
            Claim("calls", "PaymentClient.Authorize") with { OwnerId = ownerA },
            Claim("creates", "OrderFactory") with { OwnerId = ownerA },
            // Same owner, same kind, same target text as the first: exercises the ordinal counter.
            Claim("calls", "PaymentClient.Authorize") with
            {
                OwnerId = ownerA, Evidence = new Evidence(DocumentId, "Worker.cs", 2, 1, 2, 10),
            },
            Claim("references", "IEventBus") with { OwnerId = ownerB },
        };
        var resolver = RelationResolver.Default;
        var snapshot = SnapshotOf(claims);
        var legacyOrdinals = new Dictionary<string, int>(StringComparer.Ordinal);
        var expectedIds = snapshot.Claims.Select(claim => LegacyMint(legacyOrdinals, claim)).ToArray();

        var resolution = resolver.Resolve(snapshot, EmptyIndex, NoKnownFacts, CancellationToken.None);

        Assert.Equal(4, resolution.Facts.Length);
        foreach (var expected in expectedIds)
        {
            Assert.Contains(resolution.Facts, fact => fact.RelationId.Value == expected.Value);
        }
    }

    [Fact]
    public void Resolve_TheSameClaimResolvedTwoWaysWithDifferentOutcomes_MintsTheIdenticalId()
    {
        var claim = Claim("references", "PaymentClient");
        var target = new SymbolFact(
            FactHeader.Create(SymbolFactId.CreateSyntactic(ProjectId, "Client.cs", "class", "class:PaymentClient").ToFactId(), FactKind.Symbol, FactResolution.Syntactic),
            SymbolFactId.CreateSyntactic(ProjectId, "Client.cs", "class", "class:PaymentClient"),
            DocumentFactId.Create(ProjectId, "Client.cs"),
            "class",
            ContainsErrorSymbol: false,
            [], [], [],
            Semantics: null,
            Name: "PaymentClient",
            FullyQualifiedName: "global::Acme.PaymentClient",
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: "class PaymentClient",
            Arity: 0,
            ParameterTypes: []);
        var indexWithTarget = SymbolIndexBuilder.Build([target], [], [], []);
        var resolver = RelationResolver.Default;

        var resolvedFound = resolver.Resolve(SnapshotOf(claim), indexWithTarget, NoKnownFacts, CancellationToken.None);
        var resolvedMissing = resolver.Resolve(SnapshotOf(claim), EmptyIndex, NoKnownFacts, CancellationToken.None);

        var foundFact = Assert.Single(resolvedFound.Facts);
        var missingFact = Assert.Single(resolvedMissing.Facts);
        Assert.NotEqual(foundFact.Method, missingFact.Method);
        Assert.Equal(foundFact.RelationId.Value, missingFact.RelationId.Value);
    }

    [Fact]
    public void Resolve_TwoClaimsTyingOnEveryRankingSignal_BothAppearOrderedOrdinallyWithNoFirstSeenTiebreak()
    {
        var first = Claim("calls", "Foo");
        var second = Claim("calls", "Foo") with { Evidence = new Evidence(DocumentId, "Worker.cs", 5, 1, 5, 10) };
        var resolver = RelationResolver.Default;
        var snapshot = SnapshotOf(first, second);

        var resolution = resolver.Resolve(snapshot, EmptyIndex, NoKnownFacts, CancellationToken.None);

        Assert.Equal(2, resolution.Facts.Length);
        Assert.DoesNotContain("ordinal=2", resolution.Facts[0].RelationId.Value, StringComparison.Ordinal);
        Assert.Contains("ordinal=1", resolution.Facts[0].RelationId.Value, StringComparison.Ordinal);
        Assert.Contains("ordinal=2", resolution.Facts[1].RelationId.Value, StringComparison.Ordinal);
        Assert.True(string.CompareOrdinal(resolution.Facts[0].RelationId.Value, resolution.Facts[1].RelationId.Value) < 0);
    }

    [Fact]
    public void Resolve_Output_IsOrderedByRelationFactIdOrdinalComparisonNotByEvidenceOrInsertionOrder()
    {
        // Owner "AAAA" mints an id that sorts before owner "ZZZZ"'s, but is placed *second* in the
        // document (a later evidence position) - so an evidence- or insertion-preserving sort would
        // put it after the "ZZZZ" claim, while a genuine RelationFactId sort must put it first.
        var laterInDocumentButEarlierId =
            SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "method", "class:Worker/Handle/aaaa").ToFactId();
        var earlierInDocumentButLaterId =
            SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "method", "class:Worker/Handle/zzzz").ToFactId();
        var firstInDocument = Claim("references", "One") with
        {
            OwnerId = earlierInDocumentButLaterId,
            Evidence = new Evidence(DocumentId, "Worker.cs", 1, 1, 1, 5),
        };
        var secondInDocument = Claim("references", "Two") with
        {
            OwnerId = laterInDocumentButEarlierId,
            Evidence = new Evidence(DocumentId, "Worker.cs", 2, 1, 2, 5),
        };
        var resolver = RelationResolver.Default;
        var snapshot = SnapshotOf(firstInDocument, secondInDocument);
        // Confirms the accumulator's own evidence-first order places the "zzzz"-owned claim first -
        // the premise this test needs before it can show the resolver re-sorts past that order.
        Assert.Equal(
            [earlierInDocumentButLaterId, laterInDocumentButEarlierId],
            snapshot.Claims.Select(static claim => claim.OwnerId));

        var resolution = resolver.Resolve(snapshot, EmptyIndex, NoKnownFacts, CancellationToken.None);

        Assert.Equal(2, resolution.Facts.Length);
        Assert.Equal(
            resolution.Facts.OrderBy(static fact => fact.RelationId.Value, StringComparer.Ordinal).Select(static fact => fact.RelationId.Value),
            resolution.Facts.Select(static fact => fact.RelationId.Value));
        Assert.Equal(laterInDocumentButEarlierId, resolution.Facts[0].SourceId);
    }

    /// <summary>
    /// The exact pre-refactor algorithm (`RelationCollector.ClaimFor`/`NextOrdinal`, before commit
    /// `fc96d75`): a fresh per-document ordinal dictionary keyed <c>kind\0claim</c> - no owner in the
    /// key, unlike the resolver's <c>owner\0kind\0claim</c>.
    /// </summary>
    private static RelationFactId LegacyMint(Dictionary<string, int> ordinals, RawRelation claim)
    {
        var fingerprint = LegacyFingerprint(claim.Details);
        var key = $"{claim.Kind}\0{fingerprint}";
        var ordinal = ordinals.GetValueOrDefault(key) + 1;
        ordinals[key] = ordinal;
        return RelationFactId.Create(claim.OwnerId, claim.Kind, fingerprint, ordinal);
    }

    private static string LegacyFingerprint(ImmutableArray<RelationDetail> details) =>
        string.Join('|', details.Select(static detail => $"{detail.Key}={detail.Value}")).Trim();

    [Fact]
    public void Resolve_ACallsRelationResolvedThroughTheSymbolIndex_CarriesBothTheInvocationAndDeclarationSpansUnmodified()
    {
        var declarationEvidence = new Evidence(DocumentFactId.Create(ProjectId, "Target.cs"), "Target.cs", 10, 1, 12, 5);
        var target = new SymbolFact(
            FactHeader.Create(
                SymbolFactId.CreateSyntactic(ProjectId, "Target.cs", "class", "class:PaymentClient").ToFactId(),
                FactKind.Symbol, FactResolution.Syntactic, evidence: [declarationEvidence]),
            SymbolFactId.CreateSyntactic(ProjectId, "Target.cs", "class", "class:PaymentClient"),
            DocumentFactId.Create(ProjectId, "Target.cs"),
            "class",
            ContainsErrorSymbol: false,
            [], [], [],
            Semantics: null,
            Name: "PaymentClient",
            FullyQualifiedName: "global::Acme.PaymentClient",
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: "class PaymentClient",
            Arity: 0,
            ParameterTypes: []);
        var index = SymbolIndexBuilder.Build([target], [], [], []);
        var claim = Claim("references", "PaymentClient");
        var resolver = RelationResolver.Default;

        var resolution = resolver.Resolve(SnapshotOf(claim), index, NoKnownFacts, CancellationToken.None);

        var fact = Assert.Single(resolution.Facts);
        Assert.Equal(target.SymbolId.ToFactId(), fact.TargetId);
        Assert.Equal(2, fact.Header.Evidence.Length);
        Assert.Contains(claim.Evidence, fact.Header.Evidence);
        Assert.Contains(declarationEvidence, fact.Header.Evidence);
    }

    [Fact]
    public void Resolve_AnUnresolvedRelation_GainsNoEvidenceBeyondItsOwn()
    {
        var resolver = RelationResolver.Default;
        var claim = Claim("references", "NothingIndexed");

        var resolution = resolver.Resolve(SnapshotOf(claim), EmptyIndex, NoKnownFacts, CancellationToken.None);

        var fact = Assert.Single(resolution.Facts);
        Assert.Equal(ResolutionMethod.Unresolved, fact.Method);
        var evidence = Assert.Single(fact.Header.Evidence);
        Assert.Equal(claim.Evidence, evidence);
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
