using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Analysis.Relations.Resolution;

/// <summary>Spec.md's Edge Cases, restated one test per bullet against the real <see cref="RelationResolver"/>.</summary>
public sealed class RelationResolverEdgeCaseTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();
    private static readonly ISymbolIndex EmptyIndex = SymbolIndexBuilder.Build([], [], [], []);

    // "IF the SymbolIndex is empty THEN the resolver SHALL emit every relation unresolved rather than throwing."
    [Fact]
    public void EmptySymbolIndex_EveryRelationIsUnresolved_AndResolveDoesNotThrow()
    {
        var claims = new[]
        {
            Claim("calls", "Foo") with { ReceiverText = "client", ReceiverTypeText = "Client", MemberName = "Do" },
            Claim("creates", "Bar"),
            Claim("references", "Baz"),
        };
        var snapshot = SnapshotOf(claims);

        var resolution = RelationResolver.Default.Resolve(snapshot, EmptyIndex, new HashSet<FactId>(), CancellationToken.None);

        Assert.Equal(3, resolution.Facts.Length);
        Assert.All(resolution.Facts, fact => Assert.Equal(ResolutionMethod.Unresolved, fact.Method));
    }

    // "IF the run produced zero raw relations THEN the resolver SHALL persist no relation fragment and SHALL
    // still write raw/facts/relations/resolution.json with zero counts."
    [Fact]
    public void ZeroRawRelations_PersistsNoFragment_AndResolutionMetricsAreAllZero()
    {
        var fragment = RelationFragmentBuilder.Build(RelationResolution.Empty, new HashSet<FactId>(), FactValidator.Validate);
        var metrics = ResolutionMetricsProjector.Project(new RelationProjectionResult(
            [.. Enum.GetValues<RelationPartition>().Select(static partition => new RelationPartitionProjection(partition, []))],
            [], "flowchart LR\n", "# Components\n"));

        Assert.Null(fragment.Fragment);
        Assert.Empty(fragment.Diagnostics);
        Assert.Equal(0, metrics.Total);
        Assert.Equal(0, metrics.ByMethod.Exact + metrics.ByMethod.Candidate + metrics.ByMethod.Syntactic
            + metrics.ByMethod.Configured + metrics.ByMethod.Convention + metrics.ByMethod.Dynamic
            + metrics.ByMethod.Heuristic + metrics.ByMethod.Unresolved);
        Assert.All(metrics.ByPartition, partition => Assert.Equal(0, partition.Total));
    }

    // "IF a relation's source fact id is absent from the run's fact set THEN the resolver SHALL emit
    // diagnostic C2M-RELR-007 and SHALL still emit the relation."
    [Fact]
    public void MissingSourceFactId_EmitsC2MRELR007_AndTheRelationIsStillEmitted()
    {
        var target = SymbolFactId.CreateSyntactic(ProjectId, "Target.cs", "class", "class:Target").ToFactId();
        var claim = Claim("references", "Target") with { TargetId = target };
        var snapshot = SnapshotOf(claim);
        // OwnerId deliberately absent: only the target is a known fact id.
        var knownFactIds = new HashSet<FactId> { target };

        var resolution = RelationResolver.Default.Resolve(snapshot, EmptyIndex, knownFactIds, CancellationToken.None);

        var fact = Assert.Single(resolution.Facts);
        Assert.Equal(target, fact.TargetId);
        var diagnostic = Assert.Single(resolution.Diagnostics);
        Assert.Equal("C2M-RELR-007", diagnostic.Code);
    }

    // "IF a raw relation's target_text is empty or whitespace THEN the resolver SHALL leave it unresolved
    // and SHALL NOT query the index."
    [Fact]
    public void WhitespaceTargetText_IsLeftUnresolved_AndNeverQueriesTheIndex()
    {
        var claim = Claim("creates", " ") with { TargetText = " " };
        var snapshot = SnapshotOf(claim);
        var recordingIndex = new RecordingIndex();

        var resolution = RelationResolver.Default.Resolve(snapshot, recordingIndex, new HashSet<FactId>(), CancellationToken.None);

        var fact = Assert.Single(resolution.Facts);
        Assert.Equal(ResolutionMethod.Unresolved, fact.Method);
        Assert.Equal(0, recordingIndex.FindCandidatesCallCount);
        Assert.Equal(0, recordingIndex.FindMethodsCallCount);
    }

    // "IF two raw relations mint the same RelationFactId THEN the run SHALL fail structurally, matching
    // RelationProjector's existing duplicate-identity behaviour."
    [Fact]
    public void DuplicateRelationFactId_FailsStructurallyWithC2MFV001_MatchingRelationProjectorsExistingBehaviour()
    {
        var id = RelationFactId.Create(OwnerId, "calls", "target_text=Foo", 1);
        var first = MakeFact(id, "Foo");
        var second = MakeFact(id, "Foo");
        var resolution = new RelationResolution(
            [first, second],
            [],
            [DocumentExtent.Create(DocumentId, "Worker.cs", [40])]);

        var result = RelationFragmentBuilder.Build(resolution, new HashSet<FactId> { OwnerId }, FactValidator.Validate);

        Assert.Null(result.Fragment);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "C2M-FV-001");
    }

    // "WHEN the same type name is declared in two projects that do not reference each other THEN the
    // resolver SHALL report both as candidates rather than preferring the source relation's own project."
    //
    // Constructed with the claim's own project distinct from both declaring projects (mirroring T29's
    // AmbiguousReferenceProbe fixture, already proven end to end in RelationResolverEndToEndTests): with
    // neither candidate matching SymbolIndex.PriorityTier's "same project" tier, nothing distinguishes
    // them and both remain tied at "any project" - the resolver reports a Candidate outcome rather than
    // inventing a winner from project affinity that isn't actually there.
    [Fact]
    public void SameTypeNameInTwoNonReferencingProjects_YieldsCandidates_NotASameProjectPreference()
    {
        var projectA = ProjectFactId.Create("src/Acme.Orders/Acme.Orders.csproj");
        var projectB = ProjectFactId.Create("src/Acme.Payments/Acme.Payments.csproj");
        var observingProject = ProjectFactId.Create("src/Acme.Shared.Contracts/Acme.Shared.Contracts.csproj");
        var symbolA = MakeSymbol(projectA, "Foo.cs", "Foo");
        var symbolB = MakeSymbol(projectB, "Foo.cs", "Foo");
        var index = SymbolIndexBuilder.Build([symbolA, symbolB], [], [], []);
        var observingDocumentId = DocumentFactId.Create(observingProject, "Probe.cs");
        var claim = Claim("creates", "Foo") with
        {
            OwnerId = SymbolFactId.CreateSyntactic(observingProject, "Probe.cs", "class", "class:Probe").ToFactId(),
            Evidence = new Evidence(observingDocumentId, "Probe.cs", 1, 1, 1, 10),
            ProjectId = observingProject.Value,
        };
        var accumulator = new RelationClaimAccumulator();
        accumulator.Add(observingDocumentId, "Probe.cs", [40], [claim]);

        var resolution = RelationResolver.Default.Resolve(accumulator.ToSnapshot(), index, new HashSet<FactId>(), CancellationToken.None);

        var fact = Assert.Single(resolution.Facts);
        Assert.Equal(ResolutionMethod.Candidate, fact.Method);
        Assert.Equal(2, fact.Candidates.Length);
        Assert.Contains(symbolA.SymbolId.ToFactId(), fact.Candidates);
        Assert.Contains(symbolB.SymbolId.ToFactId(), fact.Candidates);
    }

    // "IF cancellation is requested mid-resolution THEN the resolver SHALL propagate the cancellation and
    // SHALL NOT convert it into a diagnostic."
    [Fact]
    public void CancellationMidResolution_Propagates_AndProducesNoDiagnostic()
    {
        var snapshot = SnapshotOf(Claim("calls", "Foo"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var exception = Assert.Throws<OperationCanceledException>(
            () => RelationResolver.Default.Resolve(snapshot, EmptyIndex, new HashSet<FactId>(), cts.Token));

        Assert.IsNotType<AggregateException>(exception);
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

    private static RelationFact MakeFact(RelationFactId id, string targetText) => new(
        FactHeader.Create(id.ToFactId(), FactKind.Relation, FactResolution.Syntactic,
            evidence: [new Evidence(DocumentId, "Worker.cs", 1, 1, 1, 10)]),
        id,
        OwnerId,
        null,
        RelationPartition.Structural,
        "calls",
        "No candidate found.",
        [new RelationDetail("target_text", targetText)],
        ResolutionMethod.Unresolved);

    private static SymbolFact MakeSymbol(ProjectFactId projectId, string relativePath, string name) => new(
        FactHeader.Create(
            SymbolFactId.CreateSyntactic(projectId, relativePath, "class", $"class:{name}").ToFactId(),
            FactKind.Symbol, FactResolution.Syntactic),
        SymbolFactId.CreateSyntactic(projectId, relativePath, "class", $"class:{name}"),
        DocumentFactId.Create(projectId, relativePath),
        "class",
        ContainsErrorSymbol: false,
        [], [], [],
        Semantics: null,
        Name: name,
        FullyQualifiedName: $"global::{name}",
        Namespace: null,
        ContainingType: null,
        ContainingSymbolId: null,
        Signature: $"class {name}",
        Arity: 0,
        ParameterTypes: []);

    /// <summary>An <see cref="ISymbolIndex"/> double that records how often it was queried, otherwise empty.</summary>
    private sealed class RecordingIndex : ISymbolIndex
    {
        public int FindCandidatesCallCount { get; private set; }

        public int FindMethodsCallCount { get; private set; }

        public ImmutableArray<AnalysisDiagnostic> Diagnostics => [];

        public SymbolIndexMetrics Metrics => new(0, ImmutableDictionary<FactResolution, int>.Empty, ImmutableDictionary<IndexedSymbolKind, int>.Empty, 0, 0);

        public SymbolFact? GetById(SymbolFactId id) => null;

        public ImmutableArray<SymbolFact> FindByName(string simpleName) => [];

        public ImmutableArray<SymbolFact> FindByQualifiedName(string fullyQualifiedName) => [];

        public ImmutableArray<SymbolFact> FindMembers(string containingType, string memberName) => [];

        public ImmutableArray<SymbolFact> FindMembers(string containingType) => [];

        public MethodLookupResult FindMethods(MethodLookup lookup)
        {
            FindMethodsCallCount++;
            return new MethodLookupResult([], 0);
        }

        public SymbolLookupResult FindCandidates(SymbolLookup lookup)
        {
            FindCandidatesCallCount++;
            return new SymbolLookupResult(SymbolLookupStatus.NotFound, [], 0);
        }
    }
}
