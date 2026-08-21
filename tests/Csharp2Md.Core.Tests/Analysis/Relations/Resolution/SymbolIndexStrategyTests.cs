using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Analysis.Relations.Resolution;

public sealed class SymbolIndexStrategyTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly ProjectFactId OtherProjectId = ProjectFactId.Create("src/Other/Other.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();
    private static readonly HashSet<FactId> NoKnownFacts = [];
    private static readonly SymbolIndexStrategy Strategy = new();

    [Fact]
    public void TryResolve_UniqueCandidate_SetsTargetIdAndSyntactic()
    {
        var target = Symbol("PaymentClient", "global::Acme.Payments.PaymentClient");
        var index = Build(target);
        var claim = Claim("references", "PaymentClient");

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.True(outcome.Handled);
        Assert.Equal(target.SymbolId.ToFactId(), outcome.TargetId);
        Assert.Equal(ResolutionMethod.Syntactic, outcome.Method);
    }

    [Fact]
    public void TryResolve_AmbiguousCandidates_LeavesTargetNullListsEveryTiedIdOrdinalOrderedAndDiagnoses()
    {
        var legacy = Symbol("PaymentService", "global::Acme.Legacy.PaymentService", projectId: ProjectId);
        var current = Symbol("PaymentService", "global::Acme.Current.PaymentService", projectId: OtherProjectId);
        var index = Build(legacy, current);
        var claim = Claim("references", "PaymentService");

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.True(outcome.Handled);
        Assert.Null(outcome.TargetId);
        Assert.Equal(ResolutionMethod.Candidate, outcome.Method);
        Assert.Equal(
            new[] { legacy, current }.Select(static s => s.SymbolId.ToFactId().Value).Order(StringComparer.Ordinal),
            outcome.Candidates.Select(static id => id.Value));
        Assert.NotNull(outcome.Diagnostic);
        Assert.Equal("C2M-RELR-002", outcome.Diagnostic!.Code);
    }

    [Fact]
    public void TryResolve_TwoTiedAtBestRankPlusALowerTierCandidate_NeverSelectsFromTheMultiEntryBestTier()
    {
        var tiedFirst = Symbol("Handler", "global::Acme.Shared.Handler", projectId: ProjectId, documentPath: "First.cs")
            with
        { Namespace = "Acme.Shared" };
        var tiedSecond = Symbol("Handler", "global::Acme.Shared.Handler", projectId: ProjectId, documentPath: "Second.cs")
            with
        { Namespace = "Acme.Shared" };
        var lowerTier = Symbol("Handler", "global::Other.Far.Handler", projectId: OtherProjectId, documentPath: "Far.cs")
            with
        { Namespace = "Other.Far" };
        var index = Build(tiedFirst, tiedSecond, lowerTier);
        var claim = Claim("references", "Handler") with { Namespace = "Acme.Shared" };

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.Null(outcome.TargetId);
        Assert.Equal(ResolutionMethod.Candidate, outcome.Method);
        Assert.Equal(2, outcome.Candidates.Length);
        Assert.DoesNotContain(lowerTier.SymbolId.ToFactId(), outcome.Candidates);
    }

    [Fact]
    public void TryResolve_NoCandidateFound_DeclinesToTheTerminalStrategy()
    {
        // The task's own "What": "nothing found declines to the terminal strategy" - UnresolvedStrategy
        // (RelationResolverTests) owns the C2M-RELR-001 diagnostic and the unresolved_reason text so
        // that behaviour is proven in exactly one place, not duplicated here.
        var index = Build(Symbol("Unrelated", "global::Acme.Unrelated"));
        var claim = Claim("references", "NothingNamedThis");

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.False(outcome.Handled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_EmptyOrWhitespaceTargetText_DeclinesWithoutQueryingTheIndex(string? targetText)
    {
        var index = new ThrowingIndex();
        var claim = Claim("references", "placeholder") with { TargetText = targetText };

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.False(outcome.Handled);
    }

    [Fact]
    public void TryResolve_NamespaceProjectAndImports_AreHintsNotFilters()
    {
        // The only candidate's own namespace differs from every hint the claim carries; a filter
        // would find nothing, a hint still resolves it.
        var target = Symbol("PaymentClient", "global::Acme.Payments.PaymentClient", projectId: OtherProjectId)
            with
        { Namespace = "Acme.Payments" };
        var index = Build(target);
        var claim = Claim("references", "PaymentClient") with
        {
            Namespace = "Somewhere.Else",
            ProjectId = ProjectId.Value,
            Imports = ["Nothing.Relevant"],
        };

        var outcome = Strategy.TryResolve(new RelationResolutionContext(claim, index, NoKnownFacts));

        Assert.True(outcome.Handled);
        Assert.Equal(target.SymbolId.ToFactId(), outcome.TargetId);
    }

    /// <summary>An <see cref="ISymbolIndex"/> that throws if queried, proving a declined claim never reaches it.</summary>
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

        public ImmutableArray<SymbolFact> FindMethods(MethodLookup lookup) =>
            throw new InvalidOperationException("Should not be queried.");

        public SymbolLookupResult FindCandidates(SymbolLookup lookup) =>
            throw new InvalidOperationException("Should not be queried.");

        public ImmutableArray<AnalysisDiagnostic> Diagnostics => [];

        public SymbolIndexMetrics Metrics => new(0, ImmutableDictionary<FactResolution, int>.Empty, ImmutableDictionary<IndexedSymbolKind, int>.Empty, 0, 0);
    }

    private static ISymbolIndex Build(params SymbolFact[] symbols) => SymbolIndexBuilder.Build(symbols, [], [], []);

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

    private static SymbolFact Symbol(
        string name,
        string fullyQualifiedName,
        ProjectFactId? projectId = null,
        string documentPath = "Feature.cs")
    {
        var owner = projectId ?? ProjectId;
        var id = SymbolFactId.CreateSyntactic(owner, documentPath, "class", fullyQualifiedName);
        return new SymbolFact(
            FactHeader.Create(id.ToFactId(), FactKind.Symbol, FactResolution.Syntactic),
            id,
            DocumentFactId.Create(owner, documentPath),
            "class",
            ContainsErrorSymbol: false,
            [],
            [],
            [],
            Semantics: null,
            Name: name,
            FullyQualifiedName: fullyQualifiedName,
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: $"class {name}",
            Arity: 0,
            ParameterTypes: []);
    }
}
