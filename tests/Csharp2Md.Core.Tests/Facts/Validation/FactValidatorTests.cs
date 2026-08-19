using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Facts.Validation;

public sealed class FactValidatorTests
{
    private static readonly ProjectFactId Project = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId Document = DocumentFactId.Create(Project, "Feature.cs");
    private static readonly TargetFactId Target = TargetFactId.Create(Project, "net10.0");
    private static readonly DocumentExtent Extent = DocumentExtent.Create(Document, "Feature.cs", [20, 10]);
    private static readonly DetectorId Detector = DetectorId.Create("io.csharp2md.http");

    [Fact]
    public void Validate_DuplicateIdsEvenWithIdenticalPayload_RejectsFragmentAndNamesRule()
    {
        var fact = Symbol("A", FactResolution.Syntactic);

        var result = Validate([fact, fact]);

        Assert.False(result.IsValid);
        Assert.Null(result.Fragment);
        Assert.Contains(result.ValidationDiagnostics, diagnostic =>
            diagnostic.Code == "C2M-FV-001" && Rule(diagnostic) == "duplicate-id" && diagnostic.ScopeId == fact.Header.Id);
    }

    [Fact]
    public void Validate_DistinctIdsWithEquivalentPayload_AcceptsBoth()
    {
        var result = Validate([Symbol("A", FactResolution.Syntactic), Symbol("B", FactResolution.Syntactic)]);

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Fragment!.Facts.Length);
    }

    [Fact]
    public void Validate_ReferenceToAbsentFact_RejectsAndNamesMissingReference()
    {
        var missingTarget = TargetFactId.Create(Project, "net9.0");
        var relation = Relation(RelationPartition.CompileTime, "type-reference", missingTarget.ToFactId(), FactResolution.Exact);

        var result = Validate([relation], knownIds: [Document.ToFactId()]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic =>
            diagnostic.Code == "C2M-FV-002" && diagnostic.Data.Any(item => item.Key == "reference" && item.Value == missingTarget.Value));
    }

    [Fact]
    public void Validate_ReferencePresentInBoundedAggregateCatalog_IsAccepted()
    {
        var relation = Relation(RelationPartition.CompileTime, "type-reference", Target.ToFactId(), FactResolution.Exact);

        var result = Validate([relation], knownIds: [Document.ToFactId(), Target.ToFactId()]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ExactSymbolDependingOnErrorSymbol_IsRejected()
    {
        var result = Validate([Symbol("A", FactResolution.Exact, containsErrorSymbol: true)]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic =>
            diagnostic.Code == "C2M-FV-003" && Rule(diagnostic) == "exact-error-symbol");
    }

    [Fact]
    public void Validate_UnresolvedSymbolDependingOnErrorSymbol_IsValidLookalike()
    {
        var result = Validate([Symbol("A", FactResolution.Unresolved, containsErrorSymbol: true)]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EvidenceForMissingDocument_IsRejected()
    {
        var otherDocument = DocumentFactId.Create(Project, "Missing.cs");
        var fact = Symbol("A", FactResolution.Syntactic, evidence: [new(otherDocument, "Missing.cs", 1, 1, 1, 2)]);

        var result = Validate([fact]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "invalid-evidence-document");
    }

    [Fact]
    public void Validate_EvidencePathOutsideItsDocument_IsRejected()
    {
        var fact = Symbol("A", FactResolution.Syntactic, evidence: [new(Document, "Other.cs", 1, 1, 1, 2)]);

        var result = Validate([fact]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "invalid-evidence-document");
    }

    [Fact]
    public void Validate_EvidencePastDocumentLineRange_IsRejected()
    {
        var fact = Symbol("A", FactResolution.Syntactic, evidence: [new(Document, "Feature.cs", 2, 1, 3, 1)]);

        var result = Validate([fact]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "invalid-evidence-range");
    }

    [Fact]
    public void Validate_EvidenceAtEndExclusiveDocumentBoundary_IsAccepted()
    {
        var fact = Symbol("A", FactResolution.Syntactic, evidence: [new(Document, "Feature.cs", 1, 1, 2, 11)]);

        var result = Validate([fact]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RuntimeRelationWithoutDetectorProvenance_IsRejected()
    {
        var relation = Relation(RelationPartition.Http, "http-request", Target.ToFactId(), FactResolution.Exact, detectorProvenance: false);

        var result = Validate([relation]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "runtime-detector-provenance");
    }

    [Fact]
    public void Validate_RuntimeRelationWithoutEvidence_IsRejected()
    {
        var relation = Relation(RelationPartition.Http, "http-request", Target.ToFactId(), FactResolution.Exact, evidence: []);

        var result = Validate([relation]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "runtime-evidence");
    }

    [Fact]
    public void Validate_InheritanceRelationWithoutDetectorProvenance_IsNowRejected()
    {
        var relation = Relation(RelationPartition.Inheritance, "implements", Target.ToFactId(), FactResolution.Syntactic, detectorProvenance: false);

        var result = Validate([relation]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "runtime-detector-provenance");
    }

    [Fact]
    public void Validate_InheritanceRelationWithoutEvidence_IsNowRejected()
    {
        var relation = Relation(RelationPartition.Inheritance, "inherits", Target.ToFactId(), FactResolution.Syntactic, evidence: []);

        var result = Validate([relation]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "runtime-evidence");
    }

    [Theory]
    [InlineData("project-reference")]
    [InlineData("package-reference")]
    public void Validate_CompileTimeOnlyRelationWithoutEvidenceOrDetectorProvenance_StillValidatesCleanly(string relationKind)
    {
        var relation = Relation(
            RelationPartition.CompileTime,
            relationKind,
            Target.ToFactId(),
            FactResolution.Exact,
            detectorProvenance: false,
            evidence: []);

        var result = Validate([relation]);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_RuntimeRelationWithDetectorProvenanceAndEvidence_IsAccepted()
    {
        var relation = Relation(RelationPartition.Http, "http-request", Target.ToFactId(), FactResolution.Exact);

        var result = Validate([relation]);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("project-reference")]
    [InlineData("package-reference")]
    public void Validate_ProjectOrPackageReferenceInRuntimePartition_IsRejected(string relationKind)
    {
        var relation = Relation(RelationPartition.Http, relationKind, Target.ToFactId(), FactResolution.Exact);

        var result = Validate([relation]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "compile-time-reference-in-runtime-partition");
    }

    [Theory]
    [InlineData("project-reference")]
    [InlineData("package-reference")]
    public void Validate_ProjectOrPackageReferenceInCompileTimePartition_IsValidLookalike(string relationKind)
    {
        var relation = Relation(RelationPartition.CompileTime, relationKind, Target.ToFactId(), FactResolution.Exact);

        var result = Validate([relation]);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Validate_NullTargetWithoutNonEmptyReason_IsRejected(string? reason)
    {
        var relation = Relation(RelationPartition.Http, "http-request", null, FactResolution.Unresolved, unresolvedReason: reason);

        var result = Validate([relation]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic => Rule(diagnostic) == "missing-unresolved-reason");
    }

    [Fact]
    public void Validate_NullTargetWithReason_IsAccepted()
    {
        var relation = Relation(
            RelationPartition.Http,
            "http-request",
            null,
            FactResolution.Unresolved,
            unresolvedReason: "No matching component was proved.");

        var result = Validate([relation]);

        Assert.True(result.IsValid);
        Assert.Equal(relation, Assert.Single(result.Fragment!.Facts));
    }

    [Fact]
    public void Validate_HeaderDiagnosticReferenceToAbsentDiagnostic_IsRejected()
    {
        var missing = DiagnosticId.Create("validation", Document.ToFactId(), "MISSING", "missing");
        var fact = Symbol("A", FactResolution.Syntactic, diagnosticIds: [missing]);

        var result = Validate([fact]);

        Assert.False(result.IsValid);
        Assert.Contains(result.ValidationDiagnostics, diagnostic =>
            diagnostic.Code == "C2M-FV-002" && diagnostic.Data.Any(item => item.Value == missing.Value));
    }

    private static FactValidationResult Validate(IEnumerable<IFact> facts, IEnumerable<FactId>? knownIds = null) =>
        FactValidator.Validate(FactValidationInput.Create(
            facts,
            documents: [Extent],
            knownFactIds: (knownIds ?? []).Concat([Project.ToFactId(), Document.ToFactId(), Target.ToFactId()])));

    private static SymbolFact Symbol(
        string signature,
        FactResolution resolution,
        bool containsErrorSymbol = false,
        IEnumerable<Evidence>? evidence = null,
        IEnumerable<DiagnosticId>? diagnosticIds = null)
    {
        var id = SymbolFactId.CreateSyntactic(Project, "Feature.cs", "class", signature);
        return new SymbolFact(
            FactHeader.Create(
                id.ToFactId(),
                FactKind.Symbol,
                resolution,
                [new FactProvenance("csharp2md", "3.0.0")],
                evidence,
                diagnosticIds),
            id,
            Document,
            "class",
            containsErrorSymbol,
            [],
            [],
            [],
            Semantics: null,
            Name: signature,
            FullyQualifiedName: $"global::{signature}",
            Namespace: null,
            ContainingType: null,
            ContainingSymbolId: null,
            Signature: signature,
            Arity: 0,
            ParameterTypes: []);
    }

    private static RelationFact Relation(
        RelationPartition partition,
        string relationKind,
        FactId? target,
        FactResolution resolution,
        bool detectorProvenance = true,
        IEnumerable<Evidence>? evidence = null,
        string? unresolvedReason = null)
    {
        var id = RelationFactId.Create(Document.ToFactId(), relationKind, "claim", 1);
        var provenance = detectorProvenance
            ? new FactProvenance("csharp2md", "3.0.0", Detector, "1.0.0")
            : new FactProvenance("csharp2md", "3.0.0");
        return new RelationFact(
            FactHeader.Create(
                id.ToFactId(),
                FactKind.Relation,
                resolution,
                [provenance],
                evidence ?? [new Evidence(Document, "Feature.cs", 1, 1, 1, 2)]),
            id,
            Document.ToFactId(),
            target,
            partition,
            relationKind,
            unresolvedReason);
    }

    private static string? Rule(AnalysisDiagnostic diagnostic) =>
        diagnostic.Data.SingleOrDefault(static item => item.Key == "rule").Value;
}
