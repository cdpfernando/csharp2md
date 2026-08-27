using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Catalogs;

public sealed class CatalogOmissionTests
{
    [Fact]
    [Trait("Requirement", "RP-21")]
    public void Project_EmptyDocument_OmitsEveryCatalog()
    {
        var view = CatalogProjectionFactory.ViewOf();

        var fragments = CatalogProjector.Project(view);

        Assert.Empty(fragments);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.StartsWith("catalogs/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-21")]
    public void Project_FamilyWithNoFacts_OmitsThatCatalog()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"));

        var fragments = CatalogProjector.Project(view);

        Assert.Equal("catalogs/entry-points.json", Assert.Single(fragments).CanonicalKey);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals("catalogs/boundary-operations.json", StringComparison.Ordinal));
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals("catalogs/contracts.json", StringComparison.Ordinal));
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Equals("catalogs/unknowns.json", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-21")]
    public void PackageProjector_EmptyDocument_PublishesNoCatalogArtifact()
    {
        var view = CatalogProjectionFactory.ViewOf();

        var fragments = new PackageProjector().Project(view, new EmptySourceReader());

        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.StartsWith("catalogs/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-21")]
    public void Project_FactOutsideCatalogFamilies_DoesNotFailProjectionValidation()
    {
        var symbol = CatalogProjectionFactory.Callable("Orphan");
        var view = CatalogProjectionFactory.ViewOf(symbol);

        var fragments = new PackageProjector().Project(view, new EmptySourceReader());

        Csharp2Md.Storage.Validation.ProjectionValidator.Validate(view, fragments);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.StartsWith("catalogs/", StringComparison.Ordinal));
        Assert.Contains(view.Slots, slot => slot.CanonicalKey == "facts/structural.json");
        Assert.Contains(fragments, fragment => fragment.CanonicalKey == "retrieval.md");
    }

    [Fact]
    [Trait("Requirement", "RP-22")]
    public void Project_EntryPoints_AreOrderedByFactIdOrdinal()
    {
        var first = CatalogProjectionFactory.CreateEntryPoint("Run", "Zeta.Api");
        var second = CatalogProjectionFactory.CreateEntryPoint("Main", "Alpha.Api");
        var view = CatalogProjectionFactory.ViewOf(first, second);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/entry-points.json");

        Assert.Equal(
            new[] { first.Reference.Id.Value, second.Reference.Id.Value }.OrderBy(static id => id, StringComparer.Ordinal),
            entries.Select(entry => entry.FactId));
    }

    [Fact]
    [Trait("Requirement", "RP-22")]
    public void Project_StoresObjectsAndFields_AreOrderedByFactIdNotFamilyConcatenation()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var view = CatalogProjectionFactory.ViewOf(store, dataObject, field);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/data-stores-objects-and-fields.json");
        var expected = new[] { store.Reference.Id.Value, dataObject.Reference.Id.Value, field.Reference.Id.Value }
            .OrderBy(static id => id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, entries.Select(entry => entry.FactId).ToArray());
        Assert.NotEqual(store.Reference.Id.Value, entries[0].FactId);
    }

    [Fact]
    [Trait("Requirement", "RP-22")]
    public void Project_SharedComponentCatalog_IsOrderedByFactId()
    {
        var component = CatalogProjectionFactory.CreateComponent("Zeta.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Alpha.Container");
        var view = CatalogProjectionFactory.ViewOf(component, unit);

        var entries = CatalogProjectionFactory.ReadCatalog(
            CatalogProjector.Project(view),
            "catalogs/components-and-deployment-units.json");

        Assert.Equal(
            new[] { component.Reference.Id.Value, unit.Reference.Id.Value }.OrderBy(static id => id, StringComparer.Ordinal),
            entries.Select(entry => entry.FactId));
    }

    [Fact]
    [Trait("Requirement", "RP-24")]
    public void Project_EveryCatalogEntryValue_IsPresentInCitedArtifact()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var (unknowns, _, _, _) = UnknownRankingTests.DegreeFixture();
        var filled = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"),
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"),
            store,
            dataObject,
            field);
        var view = PublishedPackageView.From(
            filled.Document with { Unresolved = unknowns.Document.Unresolved });

        var fragments = CatalogProjector.Project(view);
        Assert.NotEmpty(fragments);
        foreach (var fragment in fragments)
        {
            var entries = CatalogProjectionFactory.ReadCatalog([fragment], fragment.CanonicalKey);
            foreach (var entry in entries)
            {
                CatalogProjectionFactory.AssertEntryValuesPresentInCitedArtifact(view, entry);
            }
        }
    }
}
