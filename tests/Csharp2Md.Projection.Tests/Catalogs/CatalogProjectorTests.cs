using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Labels;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Projection.Source;
using Csharp2Md.Projection.Tests.Source;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Catalogs;

public sealed class CatalogProjectorTests
{
    /// <summary>
    /// Both architecture catalogs cite <c>facts/architecture.json</c>, whose ordinals run over the
    /// concatenation [components, deployment units, entry points, boundary operations, external systems].
    /// The entry-point fixture also publishes a component, so its two entries start at ordinal 1; the
    /// boundary-operation fixture publishes nothing before them, so theirs start at 0.
    /// </summary>
    [Theory]
    [Trait("Requirement", "RP-18")]
    [Trait("Requirement", "RP-19")]
    [InlineData(CatalogProjector.EntryPointsKey, 1, 2)]
    [InlineData(CatalogProjector.BoundaryOperationsKey, 0, 1)]
    public void Project_ArchitectureCatalog_EachEntryCarriesItsFactIdKeyAndFamilyOrdinal(
        string catalogKey,
        int firstOrdinal,
        int secondOrdinal)
    {
        var (view, first, second) = ArchitecturePair(catalogKey);

        var entries = CatalogProjectionFactory.ReadCatalog(CatalogProjector.Project(view), catalogKey);

        Assert.Equal(2, entries.Length);
        Assert.All(entries, entry => Assert.Equal("facts/architecture.json", entry.ArtifactKey));
        Assert.Equal([firstOrdinal, secondOrdinal], entries.Select(static entry => entry.Ordinal).ToArray());
        Assert.Contains(entries, entry => entry.FactId == first);
        Assert.Contains(entries, entry => entry.FactId == second);
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
        Assert.Equal([1, 2], entries.Select(static entry => entry.Ordinal).ToArray());
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
            composed
                .Select(fragment => fragment.CanonicalKey)
                .Take(source.Length + catalogs.Length));
        Assert.True(composed.Length >= source.Length + catalogs.Length);
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
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var view = CatalogProjectionFactory.ViewOf(
            component,
            unit,
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Worker"));

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/components-and-deployment-units.json");

        CatalogProjectionFactory.AssertEveryEntryResolves(view, entries);

        // facts/architecture.json orders components before deployment units, so these are ordinals 0 and 1
        // even though the entry point sharing the family is published after them.
        Assert.Equal(0, Assert.Single(entries, entry => entry.FactId == component.Reference.Id.Value).Ordinal);
        Assert.Equal(1, Assert.Single(entries, entry => entry.FactId == unit.Reference.Id.Value).Ordinal);
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
        Assert.All(entries, entry => Assert.Equal("facts/contract.json", entry.ArtifactKey));
        Assert.Equal([0, 1], entries.Select(static entry => entry.Ordinal).ToArray());
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

        Assert.Equal(catalogs.Select(fragment => fragment.CanonicalKey), composed.Where(fragment => fragment.CanonicalKey.StartsWith("catalogs/", StringComparison.Ordinal)).Select(fragment => fragment.CanonicalKey));
        Assert.Equal(
            [
                "catalogs/entry-points.json",
                "catalogs/boundary-operations.json",
                "catalogs/components-and-deployment-units.json",
                "catalogs/contracts.json",
            ],
            composed.Where(fragment => fragment.CanonicalKey.StartsWith("catalogs/", StringComparison.Ordinal)).Select(fragment => fragment.CanonicalKey));
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

        // facts/persistence.json orders stores, then objects, then fields.
        Assert.Equal(0, Assert.Single(entries, entry => entry.FactId == store.Reference.Id.Value).Ordinal);
        Assert.Equal(1, Assert.Single(entries, entry => entry.FactId == dataObject.Reference.Id.Value).Ordinal);
        Assert.Equal(2, Assert.Single(entries, entry => entry.FactId == field.Reference.Id.Value).Ordinal);
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
            composed.Where(fragment => fragment.CanonicalKey.StartsWith("catalogs/", StringComparison.Ordinal)).Select(fragment => fragment.CanonicalKey));
    }

    [Fact]
    [Trait("Requirement", "RP-20")]
    [Trait("Requirement", "RP-23")]
    public void Project_Unknowns_EveryEntryResolvesAgainstCitedUnresolvedOrdinal()
    {
        var (view, _, _, _) = UnknownRankingTests.DegreeFixture();

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/unknowns.json");

        CatalogProjectionFactory.AssertUnknownsResolve(view, entries);

        // The fixture's three unresolved records are each cited exactly once, so the ordinals the catalog
        // carries are a permutation of every position in the document's Unresolved array.
        Assert.Equal([0, 1, 2], entries.Select(static entry => entry.Ordinal).Order().ToArray());
    }

    [Fact]
    [Trait("Requirement", "RP-23")]
    public void Project_Unknowns_FollowsUnknownRankingOrder()
    {
        var (view, highId, midId, zeroId) = UnknownRankingTests.DegreeFixture();

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/unknowns.json");
        var ranked = UnknownRanking.Rank(view);

        Assert.Equal(
            ranked.Select(item => item.Record.Source.Id).ToArray(),
            entries.Select(entry => entry.FactId).ToArray());
        Assert.Equal([highId, midId, zeroId], entries.Select(entry => entry.FactId).ToArray());
    }

    [Fact]
    [Trait("Requirement", "RP-18")]
    [Trait("Requirement", "RP-23")]
    public void PackageProjector_PlacesUnknownsCatalogAfterPersistence()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var (unknowns, _, _, _) = UnknownRankingTests.DegreeFixture();
        var view = PublishedPackageView.From(
            unknowns.Document with
            {
                DataStores = CatalogProjectionFactory.ViewOf(store, dataObject, field).Document.DataStores,
                DataObjects = CatalogProjectionFactory.ViewOf(store, dataObject, field).Document.DataObjects,
                DataFields = CatalogProjectionFactory.ViewOf(store, dataObject, field).Document.DataFields,
            });

        var composed = new PackageProjector().Project(view, new EmptySourceReader());
        var catalogs = composed
            .Where(static fragment => fragment.CanonicalKey.StartsWith("catalogs/", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal("catalogs/data-stores-objects-and-fields.json", catalogs[^2].CanonicalKey);
        Assert.Equal("catalogs/unknowns.json", catalogs[^1].CanonicalKey);
    }

    [Fact]
    [Trait("Requirement", "GCPC-093")]
    [Trait("Requirement", "GCPC-094")]
    public void Project_EntryPoints_EntryLabelsResolveToTheCitationsOfTheirProvenValues()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entryPoint = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(component, symbol, entryPoint);

        var entries = CatalogProjectionFactory.ReadCatalog(CatalogProjector.Project(view), "catalogs/entry-points.json");
        var entry = Assert.Single(entries);

        Assert.Equal(3, entry.Labels.Length);
        AssertLabelResolves(view, entry.Labels, LabelProjector.Component, "Orders.Api", component.Reference.Id.Value);
        AssertLabelResolves(view, entry.Labels, LabelProjector.Type, "global::Acme.Orders.Host", symbol.Reference.Id.Value);
        AssertLabelResolves(view, entry.Labels, LabelProjector.Method, "Run", symbol.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-093")]
    [Trait("Requirement", "GCPC-094")]
    public void Project_BoundaryOperations_EntryLabelsIncludeProtocolVerbAndRouteCitingTheOperationItself()
    {
        var symbol = CatalogProjectionFactory.Callable("Charge");
        var component = CatalogProjectionFactory.CreateComponent("Payments.Api");
        var operation = BoundaryOperation.Create(
            symbol.Reference,
            component.Reference,
            BoundaryDirection.Inbound,
            protocol: BoundaryProtocol.Http,
            httpMethod: "GET",
            route: StructuralLiteral.Create(LiteralRole.Route, "/orders/{id}", "route"),
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "GET /orders/{id}", "protocolOperationKey"));
        var view = CatalogProjectionFactory.ViewOf(component, symbol, operation);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/boundary-operations.json");
        var entry = Assert.Single(entries);

        Assert.Equal(6, entry.Labels.Length);
        Assert.True(view.TryLocate(entry.FactId, out var self));
        foreach (var label in entry.Labels.Where(label =>
            label.Kind is LabelProjector.Protocol or LabelProjector.Verb or LabelProjector.Route))
        {
            Assert.Equal(self.ArtifactKey, label.ArtifactKey);
            Assert.Equal(self.Ordinal, label.Ordinal);
        }

        Assert.Contains(entry.Labels, label => label.Kind == LabelProjector.Protocol && label.Value == "http");
        Assert.Contains(entry.Labels, label => label.Kind == LabelProjector.Verb && label.Value == "GET");
        Assert.Contains(entry.Labels, label => label.Kind == LabelProjector.Route && label.Value == "/orders/{id}");
    }

    [Fact]
    [Trait("Requirement", "GCPC-093")]
    [Trait("Requirement", "GCPC-098")]
    public void Project_ComponentsAndDeploymentUnits_EntryLabelsResolveToTheirOwnNameCitation()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var view = CatalogProjectionFactory.ViewOf(component, unit);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/components-and-deployment-units.json");

        var componentEntry = Assert.Single(entries, entry => entry.FactId == component.Reference.Id.Value);
        AssertLabelResolves(view, componentEntry.Labels, LabelProjector.Component, "Orders.Api", component.Reference.Id.Value);

        var unitEntry = Assert.Single(entries, entry => entry.FactId == unit.Reference.Id.Value);
        AssertLabelResolves(view, unitEntry.Labels, LabelProjector.Name, "Orders.Container", unit.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-093")]
    public void Project_Contracts_EntryLabelResolvesToItsOwnNameCitation()
    {
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var view = CatalogProjectionFactory.ViewOf(contract);

        var entries = CatalogProjectionFactory.ReadCatalog(CatalogProjector.Project(view), "catalogs/contracts.json");
        var entry = Assert.Single(entries);

        AssertLabelResolves(view, entry.Labels, LabelProjector.Name, "orders.v1.OrderPlaced", contract.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-093")]
    [Trait("Requirement", "GCPC-098")]
    public void Project_PersistenceCatalog_DataStoreAndDataObjectCarryNameLabelsAndDataFieldCarriesNone()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var view = CatalogProjectionFactory.ViewOf(store, dataObject, field);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/data-stores-objects-and-fields.json");

        var storeEntry = Assert.Single(entries, entry => entry.FactId == store.Reference.Id.Value);
        AssertLabelResolves(view, storeEntry.Labels, LabelProjector.Name, "OrdersDb", store.Reference.Id.Value);

        var objectEntry = Assert.Single(entries, entry => entry.FactId == dataObject.Reference.Id.Value);
        AssertLabelResolves(view, objectEntry.Labels, LabelProjector.Name, "order_headers", dataObject.Reference.Id.Value);

        var fieldEntry = Assert.Single(entries, entry => entry.FactId == field.Reference.Id.Value);
        Assert.Empty(fieldEntry.Labels);
    }

    [Fact]
    [Trait("Requirement", "GCPC-093")]
    public void Project_Unknowns_EntryLabelIsDerivedFromTheUnresolvedOccurrencesOwnerIdentity()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unresolved = CatalogProjectionFactory.CreateUnresolved(RelationKind.Invokes, component.Reference);
        var view = CatalogProjectionFactory.ViewOf([component], unresolved: [unresolved]);

        var entries = CatalogProjectionFactory.ReadCatalog(CatalogProjector.Project(view), "catalogs/unknowns.json");
        var entry = Assert.Single(entries);

        AssertLabelResolves(view, entry.Labels, LabelProjector.Component, "Orders.Api", component.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-095")]
    public void Project_EntryPoints_OrderingStaysKeyedOnFactIdAndCanonicalIdRemainsTheIdentity()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var first = CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api");
        var second = CatalogProjectionFactory.CreateEntryPoint("Main", "Orders.Worker");
        var view = CatalogProjectionFactory.ViewOf(component, first, second);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/entry-points.json");

        var expectedOrder = new[] { first.Reference.Id.Value, second.Reference.Id.Value }
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Equal(expectedOrder, entries.Select(static entry => entry.FactId).ToArray());
    }

    /// <summary>
    /// Builds the two-fact fixture behind <paramref name="catalogKey"/> and returns the fact ids the
    /// catalog is expected to carry.
    /// </summary>
    private static (PublishedPackageView View, string First, string Second) ArchitecturePair(string catalogKey)
    {
        if (string.Equals(catalogKey, CatalogProjector.EntryPointsKey, StringComparison.Ordinal))
        {
            var run = CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api");
            var main = CatalogProjectionFactory.CreateEntryPoint("Main", "Orders.Worker");
            return (
                CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateComponent("Orders.Api"), run, main),
                run.Reference.Id.Value,
                main.Reference.Id.Value);
        }

        var charge = CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge");
        var refund = CatalogProjectionFactory.CreateInboundOperation("Refund", "Payments.Api", "POST /refund");
        return (
            CatalogProjectionFactory.ViewOf(charge, refund),
            charge.Reference.Id.Value,
            refund.Reference.Id.Value);
    }

    /// <summary>
    /// Finds the single label of <paramref name="kind"/> on <paramref name="labels"/>, asserts its value,
    /// and proves its citation is exactly the one <see cref="PublishedPackageView.TryLocate"/> assigns
    /// <paramref name="expectedFactId"/> -- the same resolution proof <c>LabelProjectorTests</c> uses,
    /// applied here to the label collection the catalog entry now carries (GCPC-093, GCPC-094).
    /// </summary>
    private static void AssertLabelResolves(
        PublishedPackageView view, ImmutableArray<LabelDto> labels, string kind, string expectedValue, string expectedFactId)
    {
        var label = Assert.Single(labels, candidate => candidate.Kind == kind);
        Assert.Equal(expectedValue, label.Value);
        Assert.True(view.TryLocate(expectedFactId, out var citation));
        Assert.Equal(citation.ArtifactKey, label.ArtifactKey);
        Assert.Equal(citation.Ordinal, label.Ordinal);
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
