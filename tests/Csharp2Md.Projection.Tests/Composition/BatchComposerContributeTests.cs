using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection.Composition;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Composition;

public sealed class BatchComposerContributeTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.Orders.slnx");
    private static readonly SolutionCoordinate Coordinate = SolutionCoordinate.For(@"C:\src\Acme.Orders.slnx");
    private const string PackageDirectory = "s-0123456789abcdef0123456789abcdef";

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void BatchComposer_ImplementsIBatchComposer()
    {
        Assert.Contains(typeof(IBatchComposer), typeof(BatchComposer).GetInterfaces());
        Assert.True(typeof(BatchComposer).IsSealed);
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void Contribute_EmitsEachCatalogFactFromTheViewExactlyOnce()
    {
        var view = ViewOf(CatalogSnapshot());
        var contribution = new BatchComposer().Contribute(view, Coordinate, PackageDirectory);

        Assert.Equal(Coordinate.Identity.Value, contribution.SolutionIdentity);
        Assert.Equal(Coordinate.SolutionFileName, contribution.SolutionFileName);
        Assert.Equal(PackageDirectory, contribution.PackageDirectory);

        Assert.Equal(view.Document.BoundaryOperations.Length, contribution.BoundaryOperations.Length);
        Assert.Equal(view.Document.Contracts.Length, contribution.Contracts.Length);
        Assert.Equal(view.Document.Components.Length, contribution.Components.Length);
        Assert.Equal(view.Document.DeploymentUnits.Length, contribution.DeploymentUnits.Length);
        Assert.Equal(view.Document.ExternalSystems.Length, contribution.ExternalSystems.Length);

        AssertDistinctFactIds(contribution.BoundaryOperations.Select(static item => item.FactId));
        AssertDistinctFactIds(contribution.Contracts.Select(static item => item.FactId));
        AssertDistinctFactIds(contribution.Components.Select(static item => item.FactId));
        AssertDistinctFactIds(contribution.DeploymentUnits.Select(static item => item.FactId));
        AssertDistinctFactIds(contribution.ExternalSystems.Select(static item => item.FactId));

        Assert.All(view.Document.BoundaryOperations, dto => Assert.Single(contribution.BoundaryOperations, item => item.FactId == dto.Identity.Id));
        Assert.All(view.Document.Contracts, dto => Assert.Single(contribution.Contracts, item => item.FactId == dto.Identity.Id));
        Assert.All(view.Document.Components, dto => Assert.Single(contribution.Components, item => item.FactId == dto.Identity.Id));
        Assert.All(view.Document.DeploymentUnits, dto => Assert.Single(contribution.DeploymentUnits, item => item.FactId == dto.Identity.Id));
        Assert.All(view.Document.ExternalSystems, dto => Assert.Single(contribution.ExternalSystems, item => item.FactId == dto.Identity.Id));
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void Contribute_ArtifactKeyAndOrdinal_MatchTryLocateForEachFactId()
    {
        var view = ViewOf(CatalogSnapshot());
        var contribution = new BatchComposer().Contribute(view, Coordinate, PackageDirectory);

        AssertLocator(view, contribution.BoundaryOperations.Select(static item => (item.FactId, item.ArtifactKey, item.Ordinal)));
        AssertLocator(view, contribution.Contracts.Select(static item => (item.FactId, item.ArtifactKey, item.Ordinal)));
        AssertLocator(view, contribution.Components.Select(static item => (item.FactId, item.ArtifactKey, item.Ordinal)));
        AssertLocator(view, contribution.DeploymentUnits.Select(static item => (item.FactId, item.ArtifactKey, item.Ordinal)));
        AssertLocator(view, contribution.ExternalSystems.Select(static item => (item.FactId, item.ArtifactKey, item.Ordinal)));
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void Contribute_FactTheViewCannotLocate_IsOmittedRatherThanEmittedWithAPlaceholderOrdinal()
    {
        var located = ViewOf(CatalogSnapshot());
        var ghost = located.Document.BoundaryOperations[0] with
        {
            Identity = new FactReferenceDto(string.Empty, "BoundaryOperation"),
        };
        var view = PublishedPackageView.From(
            located.Document with { BoundaryOperations = located.Document.BoundaryOperations.Add(ghost) });

        var contribution = new BatchComposer().Contribute(view, Coordinate, PackageDirectory);

        Assert.Equal(located.Document.BoundaryOperations.Length, contribution.BoundaryOperations.Length);
        Assert.DoesNotContain(contribution.BoundaryOperations, item => item.FactId.Length == 0);
        Assert.DoesNotContain(contribution.BoundaryOperations, item => item.FactId == ghost.Identity.Id);
        Assert.False(view.TryLocate(ghost.Identity.Id, out _));
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void Contribute_ExternalSystemNamesReadNameValue_ComponentAndDeploymentUnitNamesReadName()
    {
        var view = ViewOf(CatalogSnapshot());
        var contribution = new BatchComposer().Contribute(view, Coordinate, PackageDirectory);

        var component = Assert.Single(contribution.Components);
        Assert.Equal(view.Document.Components[0].Name, component.Name);
        Assert.NotEqual(view.Document.Components[0].Identity.Id, component.Name);
        Assert.NotEqual(view.Document.Components[0].Identity.FactType, component.Name);

        var deploymentUnit = Assert.Single(contribution.DeploymentUnits);
        Assert.Equal(view.Document.DeploymentUnits[0].Name, deploymentUnit.Name);
        Assert.NotEqual(view.Document.DeploymentUnits[0].Identity.Id, deploymentUnit.Name);

        var external = Assert.Single(contribution.ExternalSystems);
        Assert.Equal(view.Document.ExternalSystems[0].Name.Value, external.Name);
        Assert.NotEqual(view.Document.ExternalSystems[0].Name.Role, external.Name);
        Assert.NotEqual(view.Document.ExternalSystems[0].Identity.Id, external.Name);
    }

    [Fact]
    [Trait("Requirement", "MSC-25")]
    public void Contribute_CopiesBoundaryOperationDirectionProtocolKeyScopeHttpAndRoute()
    {
        var view = ViewOf(CatalogSnapshot());
        var contribution = new BatchComposer().Contribute(view, Coordinate, PackageDirectory);
        var http = Assert.Single(contribution.BoundaryOperations, item => item.HttpMethod is not null);
        var dto = Assert.Single(view.Document.BoundaryOperations, item => item.Identity.Id == http.FactId);

        Assert.Equal(dto.Identity.FactType, http.FactType);
        Assert.Equal(dto.Direction, http.Direction);
        Assert.Equal(dto.Protocol, http.Protocol);
        Assert.Equal(dto.DestinationScope, http.DestinationScope);
        Assert.Equal(dto.HttpMethod, http.HttpMethod);
        Assert.Equal(dto.Route?.Value, http.Route);
        Assert.Equal(dto.ProtocolOperationKey?.Value, http.ProtocolOperationKey);
    }

    private static PublishedPackageView ViewOf(FactualSnapshot snapshot) =>
        PublishedPackageView.From(DomainMapper.ToWire(snapshot, Context));

    private static void AssertLocator(
        PublishedPackageView view,
        IEnumerable<(string FactId, string ArtifactKey, int Ordinal)> entries)
    {
        foreach (var (factId, artifactKey, ordinal) in entries)
        {
            Assert.True(view.TryLocate(factId, out var citation));
            Assert.Equal(citation.ArtifactKey, artifactKey);
            Assert.Equal(citation.Ordinal, ordinal);
        }
    }

    private static void AssertDistinctFactIds(IEnumerable<string> factIds)
    {
        var ids = factIds.ToArray();
        Assert.Equal(ids.Distinct(StringComparer.Ordinal).Count(), ids.Length);
    }

    private static FactualSnapshot CatalogSnapshot()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.Orders.slnx");
        var projectId = ProjectId.Create(solutionId, "src/Acme.Orders/Acme.Orders.csproj");
        var signature = CanonicalSymbolSignature.Create(
            "method", "global::Acme.Orders.Checkout", "Place", 0, "global::System.Void");
        var symbol = Symbol.Create(signature, projectId, SymbolFacetSet.Create([SymbolFacet.Callable]));
        var component = Component.Create(solutionId, "ordering-api", [symbol.Reference]);
        var deploymentUnit = DeploymentUnit.Create(solutionId, "ordering-container");
        var external = ExternalSystem.Create(
            solutionId, StructuralLiteral.Create(LiteralRole.ClientName, "stripe-gateway", "name"));
        var inbound = BoundaryOperation.Create(
            symbol.Reference,
            component.Reference,
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "orders.v1.OrderPlaced", "protocolOperationKey"));
        var outbound = BoundaryOperation.Create(
            symbol.Reference,
            component.Reference,
            BoundaryDirection.Outbound,
            BoundaryProtocol.Http,
            destinationScope: "payments-client",
            httpMethod: "POST",
            route: StructuralLiteral.Create(LiteralRole.Route, "/v1/charges", "route"));
        var contract = Contract.Create(StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof"));
        return new FactualSnapshot(
            [component, deploymentUnit, external, inbound, outbound, contract],
            [],
            [],
            [],
            [],
            []);
    }
}
