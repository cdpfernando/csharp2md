using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Analysis.Relations.Resolution;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Analysis.Relations;

public sealed class RelationFragmentBuilderTests
{
    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App/App.csproj");
    private static readonly DocumentFactId DocumentId = DocumentFactId.Create(ProjectId, "Worker.cs");
    private static readonly FactId OwnerId =
        SymbolFactId.CreateSyntactic(ProjectId, "Worker.cs", "class", "class:Worker").ToFactId();

    // RELR-19/RELR-20: a raw relation carries no FactProvenance of its own (RawRelation has no such
    // field by design - only the resolver mints one, at fact-minting time), so "preserve what the claim
    // carried and append the resolver's own" collapses to exactly one entry: the resolver's. Pinned by
    // DetectorId rather than NotEmpty, so a regression that dropped or renamed it would fail here.
    [Fact]
    public void Build_EveryEvidenceAndDetailTheClaimCarried_IsPresentOnTheFactWithExactlyTheResolversOwnProvenance()
    {
        var resolution = ResolveOneClaim();

        var result = RelationFragmentBuilder.Build(resolution, new HashSet<FactId> { OwnerId }, RealValidate);

        var fact = Assert.Single(result.Fragment!.Facts.Cast<RelationFact>());
        Assert.Single(fact.Header.Evidence);
        Assert.Equal(1, fact.Header.Evidence[0].StartLine);
        Assert.Contains(fact.Details, detail => detail is { Key: "target_text", Value: "Foo" });
        var provenance = Assert.Single(fact.Header.Provenance);
        Assert.Equal(DetectorId.Create("io.csharp2md.relation-resolver"), provenance.DetectorId);
    }

    [Fact]
    public void Build_ZeroRelationResolution_YieldsNoFragmentAndNoDiagnostics()
    {
        var result = RelationFragmentBuilder.Build(RelationResolution.Empty, KnownIds(), RealValidate);

        Assert.Null(result.Fragment);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Build_AValidationFailure_IsAStructuralFailureWithTheValidatorsOwnDiagnostics()
    {
        // The relation's own SourceId is never declared as a known fact id - FactValidator.GetReferences
        // requires every RelationFact's SourceId to be either defined in the fragment or supplied through
        // knownFactIds, so C2M-FV-002 must fire, exactly as it would for the database fragment's own
        // validation failure.
        var resolution = ResolveOneClaim();

        var result = RelationFragmentBuilder.Build(resolution, new HashSet<FactId>(), RealValidate);

        Assert.Null(result.Fragment);
        Assert.NotEmpty(result.Diagnostics);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code == "C2M-FV-002");
    }

    private static FactValidationResult RealValidate(FactValidationInput input) => FactValidator.Validate(input);

    private static HashSet<FactId> KnownIds() => [];

    private static RelationResolution ResolveOneClaim()
    {
        var claim = Claim("calls", "Foo");
        var accumulator = new RelationClaimAccumulator();
        accumulator.Add(DocumentId, "Worker.cs", [40], [claim]);
        return RelationResolver.Default.Resolve(
            accumulator.ToSnapshot(),
            Csharp2Md.Core.Analysis.Indexes.SymbolIndexBuilder.Build([], [], [], []),
            new HashSet<FactId>(),
            CancellationToken.None);
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
