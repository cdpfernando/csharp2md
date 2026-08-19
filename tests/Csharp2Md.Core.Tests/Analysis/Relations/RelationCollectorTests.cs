using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Analysis.Relations;

public sealed class RelationCollectorTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();

    // Every one of the 10 in-scope kinds, its expected RelationPartition, and its observed target
    // text (the "http-call" case uses the pipe-delimited encoding SyntaxFactExtractor actually emits).
    public static TheoryData<string, RelationPartition, string> AllKinds => new()
    {
        { "calls", RelationPartition.Structural, "paymentsClient.AuthorizePayment" },
        { "creates", RelationPartition.Structural, "PaymentAuthorizer" },
        { "references", RelationPartition.Structural, "IEventBus" },
        { "inherits", RelationPartition.Inheritance, "PaymentsBase" },
        { "implements", RelationPartition.Inheritance, "IDisposable" },
        { "publishes", RelationPartition.Events, "PaymentProcessed" },
        { "subscribes", RelationPartition.Events, "OrderPlaced" },
        { "handles", RelationPartition.Events, "OrderPlaced" },
        { "http-client", RelationPartition.Http, "PaymentService" },
        { "http-call", RelationPartition.Http, "http_method=POST|route=payments/authorize" },
    };

    [Theory]
    [MemberData(nameof(AllKinds))]
    public void CreateFacts_OneCandidatePerKind_MapsToTheDocumentedPartition(
        string relationKind, RelationPartition expectedPartition, string observedTarget)
    {
        var candidates = ImmutableArray.Create(Candidate(relationKind, observedTarget, FactResolution.Syntactic));

        var facts = RelationCollector.CreateFacts(DocumentId, "Worker.cs", candidates);

        var fact = Assert.Single(facts);
        Assert.Equal(relationKind, fact.RelationKind);
        Assert.Equal(expectedPartition, fact.Partition);
    }

    [Fact]
    public void CreateFacts_EveryFact_CarriesEvidenceProvenanceNullTargetAndUnresolvedReason()
    {
        var candidates = ImmutableArray.CreateRange(
            AllKinds.Select(row => Candidate((string)row[0]!, (string)row[2]!, FactResolution.Syntactic)));

        var facts = RelationCollector.CreateFacts(DocumentId, "Worker.cs", candidates);

        Assert.Equal(candidates.Length, facts.Length);
        Assert.All(facts, fact =>
        {
            Assert.NotEmpty(fact.Header.Evidence);
            Assert.Contains(fact.Header.Provenance, provenance => provenance.DetectorId is not null);
            Assert.Null(fact.TargetId);
            Assert.False(string.IsNullOrWhiteSpace(fact.UnresolvedReason));
        });
    }

    [Fact]
    public void CreateFacts_HttpCallObservedTarget_SplitsIntoSeparateHttpMethodAndRouteDetails()
    {
        var candidates = ImmutableArray.Create(
            Candidate("http-call", "http_method=POST|route=payments/authorize", FactResolution.Syntactic));

        var fact = Assert.Single(RelationCollector.CreateFacts(DocumentId, "Worker.cs", candidates));

        Assert.Equal(2, fact.Details.Length);
        Assert.Contains(fact.Details, detail => detail is { Key: "http_method", Value: "POST" });
        Assert.Contains(fact.Details, detail => detail is { Key: "route", Value: "payments/authorize" });
        Assert.DoesNotContain(fact.Details, detail => detail.Key == "target_text");
    }

    [Fact]
    public void CreateFacts_NonHttpCallKind_WrapsObservedTargetAsOneTargetTextDetail()
    {
        var candidates = ImmutableArray.Create(Candidate("creates", "PaymentAuthorizer", FactResolution.Syntactic));

        var fact = Assert.Single(RelationCollector.CreateFacts(DocumentId, "Worker.cs", candidates));

        var detail = Assert.Single(fact.Details);
        Assert.Equal("target_text", detail.Key);
        Assert.Equal("PaymentAuthorizer", detail.Value);
    }

    [Fact]
    public void CreateFacts_TwoCandidatesSameKindAndTarget_GetDistinctOrdinalDisambiguatedIds()
    {
        var candidates = ImmutableArray.Create(
            Candidate("calls", "paymentsClient.AuthorizePayment", FactResolution.Syntactic, startLine: 10),
            Candidate("calls", "paymentsClient.AuthorizePayment", FactResolution.Syntactic, startLine: 20));

        var facts = RelationCollector.CreateFacts(DocumentId, "Worker.cs", candidates);

        Assert.Equal(2, facts.Length);
        Assert.Equal(2, facts.Select(fact => fact.RelationId.Value).Distinct().Count());
        Assert.Contains(";ordinal=1", facts[0].RelationId.Value, StringComparison.Ordinal);
        Assert.Contains(";ordinal=2", facts[1].RelationId.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateFacts_RealisticDocumentInput_PassesFactValidatorCleanly()
    {
        var candidates = ImmutableArray.CreateRange(
            AllKinds.Select(row => Candidate((string)row[0]!, (string)row[2]!, FactResolution.Syntactic)));

        var facts = RelationCollector.CreateFacts(DocumentId, "Worker.cs", candidates);

        var result = FactValidator.Validate(FactValidationInput.Create(
            facts.Cast<IFact>(),
            documents: [DocumentExtent.Create(DocumentId, "Worker.cs", Enumerable.Repeat(200, 50))],
            knownFactIds: [OwnerId]));

        Assert.True(result.IsValid, string.Join(" | ", result.ValidationDiagnostics.Select(d => d.Message)));
    }

    private static SyntacticRelationCandidate Candidate(
        string relationKind, string observedTarget, FactResolution resolution, int startLine = 1) =>
        new(OwnerId, relationKind, observedTarget, resolution, startLine, 1, startLine, 10);
}
