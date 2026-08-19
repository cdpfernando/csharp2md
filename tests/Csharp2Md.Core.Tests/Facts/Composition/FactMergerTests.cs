using Csharp2Md.Core.Facts.Composition;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Tests.Facts.Composition;

public sealed class FactMergerTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/Probe/Probe.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Probe.cs");
    private static readonly SymbolFactId FirstId = SymbolFactId.CreateSyntactic(ProjectId, "Probe.cs", "class", "class Probe");
    private static readonly SymbolFactId SecondId = SymbolFactId.CreateSyntactic(ProjectId, "Probe.cs", "class", "class Other");

    [Fact]
    public void EmptyEnrichment_RetainsEveryBaselineFact()
    {
        var baseline = new IFact[] { Document([FirstId]), Symbol(FirstId, FactResolution.Syntactic) };

        var result = FactMerger.Merge(baseline);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Facts.Length);
        Assert.Contains(result.Facts, fact => fact.Header.Id == FirstId.ToFactId());
        Assert.Contains(result.Facts, fact => fact.Header.Id == DocumentId.ToFactId());
    }

    [Fact]
    public void NewSemanticFact_IsAddedWithoutRemovingTheSyntaxFact()
    {
        var resolvedId = SymbolFactId.CreateResolved(TargetFactId.Create(ProjectId, "net10.0"), "T:Probe");

        var result = FactMerger.Merge(
            [Symbol(FirstId, FactResolution.Syntactic)],
            [Symbol(resolvedId, FactResolution.Exact)]);

        Assert.True(result.IsValid);
        Assert.Equal(
            [resolvedId, FirstId],
            result.Facts.OfType<SymbolFact>().Select(static symbol => symbol.SymbolId).ToArray());
    }

    [Fact]
    public void HigherResolution_ReplacesClaimsButUnionsProvenanceEvidenceAndDiagnostics()
    {
        var evidence = new Evidence(DocumentId, "Probe.cs", 1, 1, 1, 2);
        var diagnostic = Diagnostic("C2M-TEST-001", FirstId.ToFactId());
        var syntax = Symbol(FirstId, FactResolution.Syntactic, attributes: ["Observed"], evidence: [evidence]);
        var exact = Symbol(
            FirstId,
            FactResolution.Exact,
            attributes: ["global::ResolvedAttribute"],
            diagnostics: [diagnostic.Id],
            provenance: new("semantic", "1"));

        var result = FactMerger.Merge([syntax], [exact], [diagnostic]);

        var merged = Assert.IsType<SymbolFact>(Assert.Single(result.Facts));
        Assert.Equal(FactResolution.Exact, merged.Header.Resolution);
        Assert.Equal(["global::ResolvedAttribute"], merged.Attributes.ToArray());
        Assert.Equal(2, merged.Header.Provenance.Length);
        Assert.Equal([evidence], merged.Header.Evidence.ToArray());
        Assert.Equal([diagnostic.Id], merged.Header.DiagnosticIds.ToArray());
    }

    [Fact]
    public void LowerResolutionFailure_CannotEraseExactClaimsAndAddsItsDiagnostic()
    {
        var diagnostic = Diagnostic("C2M-TEST-002", FirstId.ToFactId());
        var exact = Symbol(FirstId, FactResolution.Exact, attributes: ["global::ResolvedAttribute"]);
        var fallback = Symbol(FirstId, FactResolution.Syntactic, attributes: ["Observed"]);

        var result = FactMerger.Merge([exact], [fallback], [diagnostic]);

        var merged = Assert.IsType<SymbolFact>(Assert.Single(result.Facts));
        Assert.Equal(FactResolution.Exact, merged.Header.Resolution);
        Assert.Equal(["global::ResolvedAttribute"], merged.Attributes.ToArray());
        Assert.Equal([diagnostic.Id], merged.Header.DiagnosticIds.ToArray());
    }

    [Fact]
    public void EquivalentExactClaims_MergeMetadataWithoutConflict()
    {
        var first = Symbol(FirstId, FactResolution.Exact, attributes: ["A"]);
        var second = Symbol(FirstId, FactResolution.Exact, attributes: ["A"], provenance: new("semantic", "2"));

        var result = FactMerger.Merge([first], [second]);

        Assert.True(result.IsValid);
        Assert.Empty(result.StructuralDiagnostics);
        Assert.Equal(2, Assert.IsType<SymbolFact>(Assert.Single(result.Facts)).Header.Provenance.Length);
    }

    [Fact]
    public void ConflictingExactClaims_AreStructuralAndNeverLastWriteWins()
    {
        var first = Symbol(FirstId, FactResolution.Exact, attributes: ["First"]);
        var second = Symbol(FirstId, FactResolution.Exact, attributes: ["Second"]);

        var result = FactMerger.Merge([first], [second]);

        Assert.False(result.IsValid);
        Assert.Equal(["First"], Assert.IsType<SymbolFact>(Assert.Single(result.Facts)).Attributes.ToArray());
        var conflict = Assert.Single(result.StructuralDiagnostics);
        Assert.Equal("C2M-FM-002", conflict.Code);
        Assert.Equal(FirstId.ToFactId(), conflict.ScopeId);
        Assert.Equal([conflict.Id], Assert.IsType<SymbolFact>(Assert.Single(result.Facts)).Header.DiagnosticIds.ToArray());
    }

    [Fact]
    public void EqualResolutionConflictingClaims_AreStructuralRatherThanOrderDependent()
    {
        var first = Symbol(FirstId, FactResolution.Syntactic, attributes: ["First"]);
        var second = Symbol(FirstId, FactResolution.Syntactic, attributes: ["Second"]);

        var forward = FactMerger.Merge([first], [second]);
        var reverse = FactMerger.Merge([second], [first]);

        Assert.False(forward.IsValid);
        Assert.False(reverse.IsValid);
        Assert.Equal("C2M-FM-003", Assert.Single(forward.StructuralDiagnostics).Code);
        Assert.Equal("C2M-FM-003", Assert.Single(reverse.StructuralDiagnostics).Code);
    }

    [Fact]
    public void DocumentWithAllExactSymbols_RecomputesExactResolution()
    {
        var result = FactMerger.Merge([
            Document([FirstId, SecondId]),
            Symbol(FirstId, FactResolution.Exact),
            Symbol(SecondId, FactResolution.Exact),
        ]);

        Assert.Equal(FactResolution.Exact, result.Facts.OfType<DocumentFact>().Single().Header.Resolution);
    }

    [Fact]
    public void DocumentWithMixedQualities_RecomputesPartialResolution()
    {
        var result = FactMerger.Merge([
            Document([FirstId, SecondId]),
            Symbol(FirstId, FactResolution.Exact),
            Symbol(SecondId, FactResolution.Syntactic),
        ]);

        Assert.Equal(FactResolution.Partial, result.Facts.OfType<DocumentFact>().Single().Header.Resolution);
    }

    [Fact]
    public void DocumentWithAllSyntacticSymbols_RecomputesSyntacticResolution()
    {
        var result = FactMerger.Merge([
            Document([FirstId, SecondId]),
            Symbol(FirstId, FactResolution.Syntactic),
            Symbol(SecondId, FactResolution.Syntactic),
        ]);

        Assert.Equal(FactResolution.Syntactic, result.Facts.OfType<DocumentFact>().Single().Header.Resolution);
    }

    [Fact]
    public void DocumentWithAllUnresolvedSymbols_RecomputesUnresolvedResolution()
    {
        var result = FactMerger.Merge([
            Document([FirstId, SecondId]),
            Symbol(FirstId, FactResolution.Unresolved),
            Symbol(SecondId, FactResolution.Unresolved),
        ]);

        Assert.Equal(FactResolution.Unresolved, result.Facts.OfType<DocumentFact>().Single().Header.Resolution);
    }

    [Fact]
    public void DocumentWithoutResolutionDependentFacts_RecomputesNotApplicable()
    {
        var result = FactMerger.Merge([Document([])]);

        Assert.Equal(FactResolution.NotApplicable, result.Facts.OfType<DocumentFact>().Single().Header.Resolution);
    }

    [Fact]
    public void ScopedDiagnostics_AreDeduplicatedAndPropagatedToAffectedDocument()
    {
        var diagnostic = Diagnostic("C2M-TEST-003", FirstId.ToFactId());

        var result = FactMerger.Merge(
            [Document([FirstId]), Symbol(FirstId, FactResolution.Unresolved)],
            diagnostics: [diagnostic, diagnostic]);

        Assert.Equal([diagnostic.Id], result.Diagnostics.Select(static item => item.Id).ToArray());
        Assert.Equal([diagnostic.Id], result.Facts.OfType<SymbolFact>().Single().Header.DiagnosticIds.ToArray());
        Assert.Equal([diagnostic.Id], result.Facts.OfType<DocumentFact>().Single().Header.DiagnosticIds.ToArray());
    }

    [Fact]
    public void SyntacticResolution_OutranksHeuristicResolutionSharingIdentity()
    {
        var heuristic = Symbol(FirstId, FactResolution.Heuristic, attributes: ["Heuristic"]);
        var syntactic = Symbol(FirstId, FactResolution.Syntactic, attributes: ["Syntactic"]);

        var result = FactMerger.Merge([heuristic], [syntactic]);

        var merged = Assert.IsType<SymbolFact>(Assert.Single(result.Facts));
        Assert.Equal(FactResolution.Syntactic, merged.Header.Resolution);
        Assert.Equal(["Syntactic"], merged.Attributes.ToArray());
    }

    [Fact]
    public void HeuristicResolution_OutranksCandidateResolutionSharingIdentity()
    {
        var candidate = Symbol(FirstId, FactResolution.Candidate, attributes: ["Candidate"]);
        var heuristic = Symbol(FirstId, FactResolution.Heuristic, attributes: ["Heuristic"]);

        var result = FactMerger.Merge([candidate], [heuristic]);

        var merged = Assert.IsType<SymbolFact>(Assert.Single(result.Facts));
        Assert.Equal(FactResolution.Heuristic, merged.Header.Resolution);
        Assert.Equal(["Heuristic"], merged.Attributes.ToArray());
    }

    [Fact]
    public void ResultOrdering_IsCanonicalAcrossInputOrder()
    {
        IFact[] first = [Document([FirstId, SecondId]), Symbol(FirstId, FactResolution.Exact), Symbol(SecondId, FactResolution.Exact)];
        IFact[] second = [first[2], first[0], first[1]];

        var forward = FactMerger.Merge(first);
        var reversed = FactMerger.Merge(second);

        Assert.Equal(
            forward.Facts.Select(static fact => fact.Header.Id),
            reversed.Facts.Select(static fact => fact.Header.Id));
    }

    private static DocumentFact Document(ImmutableArray<SymbolFactId> symbols) => new(
        Header(DocumentId.ToFactId(), FactKind.Document, FactResolution.Syntactic, new("syntax", "1")),
        DocumentId,
        ProjectId,
        "Probe.cs",
        [],
        symbols);

    private static SymbolFact Symbol(
        SymbolFactId id,
        FactResolution resolution,
        ImmutableArray<string> attributes = default,
        ImmutableArray<Evidence> evidence = default,
        ImmutableArray<DiagnosticId> diagnostics = default,
        FactProvenance? provenance = null) => new(
        FactHeader.Create(
            id.ToFactId(),
            FactKind.Symbol,
            resolution,
            [provenance ?? new FactProvenance("syntax", "1")],
            evidence.IsDefault ? [] : evidence,
            diagnostics.IsDefault ? [] : diagnostics),
        id,
        DocumentId,
        "class",
        ContainsErrorSymbol: resolution is FactResolution.Unresolved,
        [],
        attributes.IsDefault ? [] : attributes,
        [],
        Semantics: null,
        Name: "C",
        FullyQualifiedName: "global::C",
        Namespace: null,
        ContainingType: null,
        ContainingSymbolId: null,
        Signature: "class C",
        Arity: 0,
        ParameterTypes: []);

    private static FactHeader Header(FactId id, FactKind kind, FactResolution resolution, FactProvenance provenance) =>
        FactHeader.Create(id, kind, resolution, [provenance]);

    private static AnalysisDiagnostic Diagnostic(string code, FactId scopeId) =>
        AnalysisDiagnostic.Create(
            code,
            DiagnosticSeverity.Warning,
            DiagnosticStage.Document,
            scopeId,
            "Controlled merge diagnostic.");
}
