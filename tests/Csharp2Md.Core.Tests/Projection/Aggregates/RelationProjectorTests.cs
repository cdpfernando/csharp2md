using System.Text.Json;
using Csharp2Md.Core.Analysis.Contracts;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Projection.Aggregates;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

[Trait("Category", "Integration")]
public sealed class RelationProjectorTests : IDisposable
{
    private static readonly ProjectFactId Orders = ProjectFactId.Create("src/Orders/Orders.csproj");
    private static readonly ProjectFactId Payments = ProjectFactId.Create("src/Payments/Payments.csproj");
    private static readonly DocumentFactId Document = DocumentFactId.Create(Orders, "Client.cs");
    private static readonly DetectorId Detector = DetectorId.Create("io.csharp2md.test");
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-relations-").FullName;
    private readonly string _input = Directory.CreateTempSubdirectory("csharp2md-relations-input-").FullName;

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
        Directory.Delete(_input, recursive: true);
    }

    [Theory]
    [InlineData(RelationPartition.CompileTime)]
    [InlineData(RelationPartition.Inheritance)]
    [InlineData(RelationPartition.DependencyInjection)]
    [InlineData(RelationPartition.Http)]
    [InlineData(RelationPartition.Grpc)]
    [InlineData(RelationPartition.Events)]
    [InlineData(RelationPartition.Structural)]
    [InlineData(RelationPartition.Data)]
    public void Project_PlacesEachLegalRelationInItsSingleDeclaredPartition(RelationPartition partition)
    {
        var relation = Relation(partition, $"{partition}-relation", Payments.ToFactId());

        var result = RelationProjector.Project([Validated(relation)]);

        Assert.Equal(relation.RelationId.Value, Assert.Single(result.Partition(partition).Relations).RelationId);
        Assert.Equal(1, result.Partitions.Sum(static entry => entry.Relations.Length));
    }

    [Fact]
    public void Project_OrdersRelationsByCanonicalRelationIdentity()
    {
        var later = Relation(RelationPartition.Http, "request-b", Payments.ToFactId());
        var earlier = Relation(RelationPartition.Http, "request-a", Payments.ToFactId());

        var result = RelationProjector.Project([Validated(later), Validated(earlier)]);

        Assert.Equal(
            new[] { earlier.RelationId.Value, later.RelationId.Value }.Order(StringComparer.Ordinal),
            result.Partition(RelationPartition.Http).Relations.Select(static relation => relation.RelationId));
    }

    // NOTE: this test used to also assert `result.Mermaid == "flowchart LR\n"` for a null-target relation.
    // RelationProjectionResult no longer carries Mermaid (moved to ComponentGraphProjector); that half of
    // the scenario has a strictly stronger existing replacement -
    // ComponentGraphProjectorTests.Project_RelationWithNullTargetId_IsDropped, which asserts the edge set
    // directly instead of a rendered string. The partition-level assertions below (TargetId/UnresolvedReason
    // survive projection) are still RelationProjector's own job and are unchanged.
    [Fact]
    public void Project_PreservesUnresolvedTargetAndReason()
    {
        var relation = Relation(RelationPartition.Http, "http-request", null, "Remote endpoint was not proved.");
        var result = RelationProjector.Project([Validated(relation)]);

        var projected = Assert.Single(result.Partition(RelationPartition.Http).Relations);
        Assert.Null(projected.TargetId);
        Assert.Equal("Remote endpoint was not proved.", projected.UnresolvedReason);
    }

    [Fact]
    public void Project_RejectsDuplicateRelationIdentityAcrossValidatedFragments()
    {
        var relation = Relation(RelationPartition.CompileTime, "project-reference", Payments.ToFactId());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RelationProjector.Project([Validated(relation), Validated(relation)]));

        Assert.Contains("Duplicate relation identity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_RejectsUnsupportedRelationPartition()
    {
        var relation = Relation((RelationPartition)99, "unknown", Payments.ToFactId());

        var exception = Assert.Throws<InvalidOperationException>(() => RelationProjector.Project([Validated(relation)]));

        Assert.Contains("Unsupported relation partition", exception.Message, StringComparison.Ordinal);
    }

    // NOTE: this test used to also assert the written dependencies.mmd/components.md equalled
    // result.Mermaid/result.ComponentIndex. RelationProjectionResult no longer carries either (moved to
    // ComponentGraphProjector via AggregateOutputSnapshot.Graph, wired in T10/T11), so only the partition
    // JSON assertion - still entirely RelationProjector's own job - survives here.
    [Fact]
    public void Writer_EmitsProjectedPartitionJson()
    {
        var relation = Relation(RelationPartition.CompileTime, "project-reference", Payments.ToFactId());
        var result = RelationProjector.Project([Validated(relation)]);

        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(result), TimeProvider.System);

        using var partition = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "raw", "facts", "relations", "compile-time.json")));
        Assert.Equal(relation.RelationId.Value, Assert.Single(partition.RootElement.GetProperty("entries").EnumerateArray()).GetProperty("relation_id").GetString());
    }

    // NOTE: this snapshot used to also capture Mermaid and Components, both hardcoded literals now that
    // RelationProjector no longer produces them (see Writer_EmitsProjectedPartitionJson's note). Only the
    // partition JSON - still RelationProjector's own output - remains worth pinning here.
    [Fact]
    public Task Writer_ProjectedOutputsMatchApprovedSnapshot()
    {
        var relation = Relation(RelationPartition.CompileTime, "project-reference", Payments.ToFactId());
        var result = RelationProjector.Project([Validated(relation)]);
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(result), TimeProvider.System);

        return Verifier.Verify(new
        {
            CompileTime = File.ReadAllText(Path.Combine(_root, "raw", "facts", "relations", "compile-time.json")),
        }).UseDirectory("snapshots");
    }

    private static AggregateOutputSnapshot Snapshot(RelationProjectionResult relations) => new(
        "architecture", "system-design", "3.0.0", AnalysisMode.SyntaxOnly, AnalysisMode.SyntaxOnly,
        TrustMode.Untrusted, [], new ManifestCoverage(1, 2, 1), [], Relations: relations);

    private static ValidatedFactFragment Validated(params IFact[] facts)
    {
        var result = FactValidator.Validate(FactValidationInput.Create(
            facts,
            documents: [DocumentExtent.Create(Document, "Client.cs", [80])],
            knownFactIds: [Orders.ToFactId(), Payments.ToFactId(), Document.ToFactId()]));
        return Assert.IsType<ValidatedFactFragment>(result.Fragment);
    }

    private static RelationFact Relation(
        RelationPartition partition,
        string kind,
        FactId? target,
        string? unresolvedReason = null)
    {
        var id = RelationFactId.Create(Orders.ToFactId(), kind, $"target={target?.Value ?? "none"}", 1);
        var requiresEvidence = kind is not ("project-reference" or "package-reference");
        return new RelationFact(
            FactHeader.Create(
                id.ToFactId(),
                FactKind.Relation,
                target is null ? FactResolution.Unresolved : FactResolution.Exact,
                [new FactProvenance("test", "1", requiresEvidence ? Detector : null, requiresEvidence ? "1" : null)],
                requiresEvidence ? [new Evidence(Document, "Client.cs", 1, 1, 1, 2)] : []),
            id,
            Orders.ToFactId(),
            target,
            partition,
            kind,
            unresolvedReason,
            default);
    }
}
