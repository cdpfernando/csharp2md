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

    [Fact]
    public void Project_PreservesUnresolvedTargetAndReasonWithoutCreatingMermaidNode()
    {
        var relation = Relation(RelationPartition.Http, "http-request", null, "Remote endpoint was not proved.");
        var result = RelationProjector.Project([Validated(relation)]);

        var projected = Assert.Single(result.Partition(RelationPartition.Http).Relations);
        Assert.Null(projected.TargetId);
        Assert.Equal("Remote endpoint was not proved.", projected.UnresolvedReason);
        Assert.Equal("flowchart LR\n", result.Mermaid);
    }

    [Fact]
    public void Project_NeverPromotesLogicalDetailToMermaidIdentity()
    {
        var relation = Relation(
            RelationPartition.Http,
            "http-request",
            null,
            "Remote endpoint was not proved.",
            [new RelationDetail("logical_destination", "payments-api")]);

        var result = RelationProjector.Project([Validated(relation), Validated(Component("service/web-api", Orders))]);

        Assert.DoesNotContain("payments-api", result.Mermaid, StringComparison.Ordinal);
        Assert.Equal("flowchart LR\n", result.Mermaid);
    }

    [Fact]
    public void Project_IndexesComponentsAndProjectsInCanonicalOrder()
    {
        var payments = Component("library", Payments);
        var orders = Component("service/web-api", Orders);

        var result = RelationProjector.Project([Validated(payments), Validated(orders)]);

        Assert.Equal(
            new[] { orders.ComponentId.Value, payments.ComponentId.Value }.Order(StringComparer.Ordinal),
            result.Components.Select(static component => component.ComponentId));
        Assert.Contains($"- project: `{Orders.Value}`", result.ComponentIndex, StringComparison.Ordinal);
        Assert.Contains($"- project: `{Payments.Value}`", result.ComponentIndex, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_BuildsMermaidOnlyFromComponentMappedFactualProjectRelations()
    {
        var relation = Relation(RelationPartition.CompileTime, "project-reference", Payments.ToFactId());
        var orders = Component("service/web-api", Orders);
        var payments = Component("library", Payments);

        var result = RelationProjector.Project([Validated(relation, orders, payments)]);

        Assert.Contains(orders.ComponentId.Value, result.Mermaid, StringComparison.Ordinal);
        Assert.Contains(payments.ComponentId.Value, result.Mermaid, StringComparison.Ordinal);
        Assert.Contains("compile-time:project-reference", result.Mermaid, StringComparison.Ordinal);
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
    public void Project_RejectsDuplicateComponentIdentityAcrossValidatedFragments()
    {
        var component = Component("library", Orders);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RelationProjector.Project([Validated(component), Validated(component)]));

        Assert.Contains("Duplicate component identity", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_RejectsAProjectClaimedBySeveralComponents()
    {
        var relation = Relation(RelationPartition.CompileTime, "project-reference", Payments.ToFactId());
        var root = Component("service/web-api", Orders);
        var duplicateOwner = Component("library", Orders, Payments);
        var target = Component("library", Payments);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RelationProjector.Project([Validated(relation, root, duplicateOwner, target)]));

        Assert.Contains("belongs to more than one component", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Project_RejectsUnsupportedRelationPartition()
    {
        var relation = Relation((RelationPartition)99, "unknown", Payments.ToFactId());

        var exception = Assert.Throws<InvalidOperationException>(() => RelationProjector.Project([Validated(relation)]));

        Assert.Contains("Unsupported relation partition", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Writer_EmitsProjectedPartitionComponentIndexAndMermaid()
    {
        var relation = Relation(RelationPartition.CompileTime, "project-reference", Payments.ToFactId());
        var result = RelationProjector.Project(
            [Validated(relation, Component("service/web-api", Orders), Component("library", Payments))]);

        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(result), TimeProvider.System);

        using var partition = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "raw", "facts", "relations", "compile-time.json")));
        Assert.Equal(relation.RelationId.Value, Assert.Single(partition.RootElement.GetProperty("entries").EnumerateArray()).GetProperty("relation_id").GetString());
        Assert.Equal(result.Mermaid, File.ReadAllText(Path.Combine(_root, "raw", "dependencies.mmd")));
        Assert.Equal(result.ComponentIndex, File.ReadAllText(Path.Combine(_root, "raw", "codebase", "components.md")));
    }

    [Fact]
    public Task Writer_ProjectedOutputsMatchApprovedSnapshot()
    {
        var relation = Relation(RelationPartition.CompileTime, "project-reference", Payments.ToFactId());
        var result = RelationProjector.Project(
            [Validated(relation, Component("service/web-api", Orders), Component("library", Payments))]);
        new CanonicalAggregateWriter().Write(_root, _input, false, Snapshot(result), TimeProvider.System);

        return Verifier.Verify(new
        {
            Mermaid = File.ReadAllText(Path.Combine(_root, "raw", "dependencies.mmd")),
            Components = File.ReadAllText(Path.Combine(_root, "raw", "codebase", "components.md")),
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

    private static ComponentFact Component(string kind, params ProjectFactId[] projects)
    {
        var id = ComponentFactId.Create(kind, projects.Select(static project => project.ToFactId()));
        return new ComponentFact(
            FactHeader.Create(id.ToFactId(), FactKind.Component, FactResolution.Exact, [new FactProvenance("test", "1")]),
            id,
            kind,
            projects.ToImmutableArray());
    }

    private static RelationFact Relation(
        RelationPartition partition,
        string kind,
        FactId? target,
        string? unresolvedReason = null,
        ImmutableArray<RelationDetail> details = default)
    {
        var id = RelationFactId.Create(Orders.ToFactId(), kind, $"target={target?.Value ?? "none"}", 1);
        var runtime = partition is RelationPartition.DependencyInjection or RelationPartition.Http or RelationPartition.Grpc or RelationPartition.Events;
        return new RelationFact(
            FactHeader.Create(
                id.ToFactId(),
                FactKind.Relation,
                target is null ? FactResolution.Unresolved : FactResolution.Exact,
                [new FactProvenance("test", "1", runtime ? Detector : null, runtime ? "1" : null)],
                runtime ? [new Evidence(Document, "Client.cs", 1, 1, 1, 2)] : []),
            id,
            Orders.ToFactId(),
            target,
            partition,
            kind,
            unresolvedReason,
            details);
    }
}
