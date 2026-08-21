using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Relations.Resolution;

public sealed class ReceiverTypeStrategyTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();
    private static readonly HashSet<FactId> NoKnownFacts = [];
    private static readonly ReceiverTypeStrategy Strategy = new();

    [Fact]
    public void TryResolve_ExactlyOneMethodAtTheTopRank_SetsTargetIdAndSyntactic()
    {
        var authorize = Method("Authorize", "global::Acme.Payments.PaymentClient", []);
        var index = Build(authorize);
        var claim = CallsClaim(receiverText: "paymentClient", receiverType: "global::Acme.Payments.PaymentClient", memberName: "Authorize");

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.True(outcome.Handled);
        Assert.Equal(authorize.SymbolId.ToFactId(), outcome.TargetId);
        Assert.Equal(ResolutionMethod.Syntactic, outcome.Method);
    }

    [Fact]
    public void TryResolve_MoreThanOneMethodAtTheTopRank_YieldsCandidateWithEveryTiedIdNeverAPick()
    {
        var first = Method("Authorize", "global::Acme.Payments.PaymentClient", [], discriminator: "one");
        var second = Method("Authorize", "global::Acme.Payments.PaymentClient", [], discriminator: "two");
        var index = Build(first, second);
        var claim = CallsClaim(receiverText: "paymentClient", receiverType: "global::Acme.Payments.PaymentClient", memberName: "Authorize");

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.True(outcome.Handled);
        Assert.Null(outcome.TargetId);
        Assert.Equal(ResolutionMethod.Candidate, outcome.Method);
        Assert.Equal(
            new[] { first, second }.Select(static s => s.SymbolId.ToFactId().Value).Order(StringComparer.Ordinal),
            outcome.Candidates.Select(static id => id.Value));
        Assert.Equal("C2M-RELR-002", outcome.Diagnostic!.Code);
    }

    [Fact]
    public void TryResolve_UndeterminableReceiverType_YieldsUnresolvedAndDiagnosesC2MRELR003()
    {
        var index = Build();
        var claim = CallsClaim(receiverText: "someField", receiverType: null, memberName: "Authorize");

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.True(outcome.Handled);
        Assert.Null(outcome.TargetId);
        Assert.Equal(ResolutionMethod.Unresolved, outcome.Method);
        Assert.Equal("C2M-RELR-003", outcome.Diagnostic!.Code);
    }

    [Fact]
    public void TryResolve_ARelationKindOtherThanCalls_IsDeclinedWithoutALookup()
    {
        var index = new ThrowingIndex();
        var claim = CallsClaim(receiverText: "paymentClient", receiverType: "global::Acme.Payments.PaymentClient", memberName: "Authorize")
            with
        { Kind = "creates" };

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.False(outcome.Handled);
    }

    [Fact]
    public void TryResolve_CallsWithNoReceiver_DeclinesLettingSymbolIndexStrategyTry()
    {
        var index = new ThrowingIndex();
        var claim = CallsClaim(receiverText: null, receiverType: null, memberName: "Authorize");

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.False(outcome.Handled);
    }

    private sealed class ThrowingIndex : ISymbolIndex
    {
        public SymbolFact? GetById(SymbolFactId id) => throw new InvalidOperationException("Should not be queried.");

        public ImmutableArray<SymbolFact> FindByName(string simpleName) =>
            throw new InvalidOperationException("Should not be queried.");

        public ImmutableArray<SymbolFact> FindByQualifiedName(string fullyQualifiedName) =>
            throw new InvalidOperationException("Should not be queried.");

        public ImmutableArray<SymbolFact> FindMembers(string containingType, string memberName) =>
            throw new InvalidOperationException("Should not be queried.");

        public ImmutableArray<SymbolFact> FindMembers(string containingType) =>
            throw new InvalidOperationException("Should not be queried.");

        public MethodLookupResult FindMethods(MethodLookup lookup) =>
            throw new InvalidOperationException("Should not be queried.");

        public SymbolLookupResult FindCandidates(SymbolLookup lookup) =>
            throw new InvalidOperationException("Should not be queried.");

        public ImmutableArray<AnalysisDiagnostic> Diagnostics => [];

        public SymbolIndexMetrics Metrics => new(0, ImmutableDictionary<FactResolution, int>.Empty, ImmutableDictionary<IndexedSymbolKind, int>.Empty, 0, 0);
    }

    private static ISymbolIndex Build(params SymbolFact[] symbols) => SymbolIndexBuilder.Build(symbols, [], [], []);

    private static RawRelation CallsClaim(string? receiverText, string? receiverType, string? memberName) => new()
    {
        Kind = "calls",
        OwnerId = OwnerId,
        Evidence = new Evidence(DocumentId, "Worker.cs", 1, 1, 1, 10),
        ShapeConfidence = FactResolution.Syntactic,
        Partition = RelationPartition.Structural,
        Details = [new RelationDetail("target_text", $"{receiverText}.{memberName}")],
        TargetText = $"{receiverText}.{memberName}",
        ReceiverText = receiverText,
        ReceiverTypeText = receiverType,
        MemberName = memberName,
        ArgumentCount = 0,
    };

    private static SymbolFact Method(
        string name, string containingType, ImmutableArray<string> parameterTypes, string discriminator = "one")
    {
        var id = SymbolFactId.CreateSyntactic(ProjectId, "Feature.cs", "method", $"{containingType}/{name}/{discriminator}");
        return new SymbolFact(
            FactHeader.Create(id.ToFactId(), FactKind.Symbol, FactResolution.Syntactic),
            id,
            DocumentFactId.Create(ProjectId, "Feature.cs"),
            "method",
            ContainsErrorSymbol: false,
            [],
            [],
            [],
            Semantics: null,
            Name: name,
            FullyQualifiedName: $"{containingType}.{name}",
            Namespace: null,
            ContainingType: containingType,
            ContainingSymbolId: null,
            Signature: $"{containingType}.{name}()",
            Arity: 0,
            ParameterTypes: parameterTypes);
    }
}
