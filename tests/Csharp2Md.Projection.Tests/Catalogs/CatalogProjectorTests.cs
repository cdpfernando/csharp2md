using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Source;
using Csharp2Md.Projection.Tests.Source;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Catalogs;

public sealed class CatalogProjectorTests
{
    [Fact]
    [Trait("Requirement", "RP-18")]
    [Trait("Requirement", "RP-19")]
    public void Project_EntryPoints_EachEntryCarriesFactIdKeyAndZeroBasedOrdinal()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var first = CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api");
        var second = CatalogProjectionFactory.CreateEntryPoint("Main", "Orders.Worker");
        var view = CatalogProjectionFactory.ViewOf(component, first, second);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/entry-points.json");

        Assert.Equal(2, entries.Length);
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrEmpty(entry.FactId));
            Assert.False(string.IsNullOrEmpty(entry.ArtifactKey));
            Assert.True(entry.Ordinal >= 0, entry.FactId);
        });
        Assert.Contains(entries, entry => entry.FactId == first.Reference.Id.Value);
        Assert.Contains(entries, entry => entry.FactId == second.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    [Trait("Requirement", "RP-19")]
    public void Project_BoundaryOperations_EachEntryCarriesFactIdKeyAndZeroBasedOrdinal()
    {
        var first = CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge");
        var second = CatalogProjectionFactory.CreateInboundOperation("Refund", "Payments.Api", "POST /refund");
        var view = CatalogProjectionFactory.ViewOf(first, second);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/boundary-operations.json");

        Assert.Equal(2, entries.Length);
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrEmpty(entry.FactId));
            Assert.False(string.IsNullOrEmpty(entry.ArtifactKey));
            Assert.True(entry.Ordinal >= 0, entry.FactId);
        });
        Assert.Contains(entries, entry => entry.FactId == first.Reference.Id.Value);
        Assert.Contains(entries, entry => entry.FactId == second.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-20")]
    public void Project_EntryPointEntries_ResolveAgainstCitedArtifactOrdinal()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var first = CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api");
        var second = CatalogProjectionFactory.CreateEntryPoint("Main", "Orders.Worker");
        var view = CatalogProjectionFactory.ViewOf(component, first, second);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/entry-points.json");

        CatalogProjectionFactory.AssertEveryEntryResolves(view, entries);
        Assert.DoesNotContain(entries, entry => entry.Ordinal == 0);
    }

    [Fact]
    [Trait("Requirement", "RP-20")]
    public void Project_BoundaryOperationEntries_ResolveAgainstCitedArtifactOrdinal()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"),
            CatalogProjectionFactory.CreateInboundOperation("Refund", "Payments.Api", "POST /refund"));

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/boundary-operations.json");

        CatalogProjectionFactory.AssertEveryEntryResolves(view, entries);
    }

    [Fact]
    [Trait("Requirement", "RP-19")]
    [Trait("Requirement", "RP-20")]
    public void Project_EntryCitations_MatchPublishedPackageViewTryLocate()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"));

        var fragments = CatalogProjector.Project(view);
        var entryPoints = CatalogProjectionFactory.ReadCatalog(fragments, "catalogs/entry-points.json");
        var operations = CatalogProjectionFactory.ReadCatalog(fragments, "catalogs/boundary-operations.json");

        CatalogProjectionFactory.AssertEveryEntryResolves(view, entryPoints);
        CatalogProjectionFactory.AssertEveryEntryResolves(view, operations);
        foreach (var entry in entryPoints.Concat(operations))
        {
            Assert.True(view.TryLocate(entry.FactId, out var citation));
            Assert.Equal(citation, new ArtifactCitation(entry.ArtifactKey, entry.Ordinal));
        }
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    public void PackageProjector_ComposesCatalogProjectorAfterSource()
    {
        var (sourceView, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", "class Program;"u8.ToArray()));
        var architecture = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"));
        var view = PublishedPackageView.From(
            sourceView.Document with
            {
                EntryPoints = architecture.Document.EntryPoints,
                BoundaryOperations = architecture.Document.BoundaryOperations,
            });

        var composed = new PackageProjector().Project(view, reader);
        var source = SourceProjector.Project(view, reader);
        var catalogs = CatalogProjector.Project(view);

        Assert.Equal(
            source.Select(fragment => fragment.CanonicalKey).Concat(catalogs.Select(fragment => fragment.CanonicalKey)),
            composed.Select(fragment => fragment.CanonicalKey));
        Assert.Equal(source.Length + catalogs.Length, composed.Length);
        Assert.Equal("catalogs/entry-points.json", composed[source.Length].CanonicalKey);
        Assert.Equal("catalogs/boundary-operations.json", composed[source.Length + 1].CanonicalKey);
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    public void Project_ComponentsAndDeploymentUnits_ShareOneCatalog()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var view = CatalogProjectionFactory.ViewOf(component, unit);

        var fragments = CatalogProjector.Project(view);
        var entries = CatalogProjectionFactory.ReadCatalog(fragments, "catalogs/components-and-deployment-units.json");

        Assert.Equal(2, entries.Length);
        Assert.Contains(entries, entry => entry.FactId == component.Reference.Id.Value);
        Assert.Contains(entries, entry => entry.FactId == unit.Reference.Id.Value);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals("catalogs/components.json", StringComparison.Ordinal));
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals("catalogs/deployment-units.json", StringComparison.Ordinal));
        Assert.Single(fragments);
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    [Trait("Requirement", "RP-20")]
    public void Project_ComponentsAndDeploymentUnits_EveryEntryResolves()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container"),
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Worker"));

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/components-and-deployment-units.json");

        CatalogProjectionFactory.AssertEveryEntryResolves(view, entries);
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrEmpty(entry.FactId));
            Assert.False(string.IsNullOrEmpty(entry.ArtifactKey));
            Assert.True(entry.Ordinal >= 0, entry.FactId);
        });
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    public void Project_Contracts_AreASeparateCatalog()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var view = CatalogProjectionFactory.ViewOf(component, unit, contract);

        var fragments = CatalogProjector.Project(view);
        var shared = CatalogProjectionFactory.ReadCatalog(fragments, "catalogs/components-and-deployment-units.json");
        var contracts = CatalogProjectionFactory.ReadCatalog(fragments, "catalogs/contracts.json");

        Assert.DoesNotContain(shared, entry => entry.FactId == contract.Reference.Id.Value);
        Assert.Equal(contract.Reference.Id.Value, Assert.Single(contracts).FactId);
        Assert.Equal(2, fragments.Length);
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    [Trait("Requirement", "RP-19")]
    [Trait("Requirement", "RP-20")]
    public void Project_ContractEntries_CarryCitationAndResolveAgainstCitedArtifact()
    {
        var first = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var second = CatalogProjectionFactory.CreateContract("payments.v1.Charge");
        var view = CatalogProjectionFactory.ViewOf(first, second);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/contracts.json");

        Assert.Equal(2, entries.Length);
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrEmpty(entry.FactId));
            Assert.False(string.IsNullOrEmpty(entry.ArtifactKey));
            Assert.True(entry.Ordinal >= 0, entry.FactId);
        });
        CatalogProjectionFactory.AssertEveryEntryResolves(view, entries);
        Assert.Contains(entries, entry => entry.FactId == first.Reference.Id.Value);
        Assert.Contains(entries, entry => entry.FactId == second.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-19")]
    [Trait("Requirement", "RP-20")]
    public void Project_SharedArchitectureCatalog_CitationsMatchTryLocate()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container"));

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/components-and-deployment-units.json");

        foreach (var entry in entries)
        {
            Assert.True(view.TryLocate(entry.FactId, out var citation));
            Assert.Equal(citation, new ArtifactCitation(entry.ArtifactKey, entry.Ordinal));
            Assert.Equal(entry.FactId, CatalogProjectionFactory.FactIdAt(view, citation));
        }
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    public void PackageProjector_PlacesComponentAndContractCatalogsAfterBoundaryOperations()
    {
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"),
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"));

        var composed = new PackageProjector().Project(view, new EmptySourceReader());
        var catalogs = CatalogProjector.Project(view);

        Assert.Equal(catalogs.Select(fragment => fragment.CanonicalKey), composed.Select(fragment => fragment.CanonicalKey));
        Assert.Equal(
            [
                "catalogs/entry-points.json",
                "catalogs/boundary-operations.json",
                "catalogs/components-and-deployment-units.json",
                "catalogs/contracts.json",
            ],
            composed.Select(fragment => fragment.CanonicalKey));
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    public void Project_StoresObjectsAndFields_AppearInOneCatalog()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var view = CatalogProjectionFactory.ViewOf(store, dataObject, field);

        var fragments = CatalogProjector.Project(view);
        var entries = CatalogProjectionFactory.ReadCatalog(fragments, "catalogs/data-stores-objects-and-fields.json");

        Assert.Single(fragments);
        Assert.Equal(3, entries.Length);
        Assert.Contains(entries, entry => entry.FactId == store.Reference.Id.Value);
        Assert.Contains(entries, entry => entry.FactId == dataObject.Reference.Id.Value);
        Assert.Contains(entries, entry => entry.FactId == field.Reference.Id.Value);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals("catalogs/data-stores.json", StringComparison.Ordinal));
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals("catalogs/data-objects.json", StringComparison.Ordinal));
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals("catalogs/data-fields.json", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    public void Project_StoresObjectsAndFields_EachEntryIsLabelledByFactType()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var view = CatalogProjectionFactory.ViewOf(store, dataObject, field);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/data-stores-objects-and-fields.json");

        Assert.Equal("DataStore", Assert.Single(entries, entry => entry.FactId == store.Reference.Id.Value).FactType);
        Assert.Equal("DataObject", Assert.Single(entries, entry => entry.FactId == dataObject.Reference.Id.Value).FactType);
        Assert.Equal("DataField", Assert.Single(entries, entry => entry.FactId == field.Reference.Id.Value).FactType);
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    [Trait("Requirement", "RP-20")]
    public void Project_StoresObjectsAndFields_EveryEntryResolves()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var view = CatalogProjectionFactory.ViewOf(store, dataObject, field);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/data-stores-objects-and-fields.json");

        CatalogProjectionFactory.AssertEveryEntryResolves(view, entries);
        Assert.All(entries, entry =>
        {
            Assert.False(string.IsNullOrEmpty(entry.FactId));
            Assert.False(string.IsNullOrEmpty(entry.ArtifactKey));
            Assert.True(entry.Ordinal >= 0, entry.FactId);
        });
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    public void Project_DataOperation_IsOmittedFromStoresObjectsAndFieldsCatalog()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var operation = CatalogProjectionFactory.CreateOperation(dataObject);
        var view = CatalogProjectionFactory.ViewOf(store, dataObject, field, operation);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/data-stores-objects-and-fields.json");

        Assert.DoesNotContain(entries, entry => entry.FactId == operation.Reference.Id.Value);
        Assert.Equal(3, entries.Length);
    }

    [Fact]
    [Trait("Requirement", "RP-19")]
    [Trait("Requirement", "RP-20")]
    public void Project_PersistenceCatalog_CitationsMatchTryLocate()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var view = CatalogProjectionFactory.ViewOf(store, dataObject, field);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/data-stores-objects-and-fields.json");

        foreach (var entry in entries)
        {
            Assert.True(view.TryLocate(entry.FactId, out var citation));
            Assert.Equal(citation, new ArtifactCitation(entry.ArtifactKey, entry.Ordinal));
            Assert.Equal(entry.FactId, CatalogProjectionFactory.FactIdAt(view, citation));
        }
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    public void PackageProjector_PlacesPersistenceCatalogAfterContracts()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"),
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"),
            store,
            dataObject,
            field);

        var composed = new PackageProjector().Project(view, new EmptySourceReader());

        Assert.Equal(
            [
                "catalogs/entry-points.json",
                "catalogs/boundary-operations.json",
                "catalogs/components-and-deployment-units.json",
                "catalogs/contracts.json",
                "catalogs/data-stores-objects-and-fields.json",
            ],
            composed.Select(fragment => fragment.CanonicalKey));
    }
}

internal sealed class EmptySourceReader : ISourceDocumentReader
{
    public ImmutableArray<DocumentId> Documents => [];

    public bool TryRead(DocumentId document, out ImmutableArray<byte> bytes)
    {
        bytes = default;
        return false;
    }
}
