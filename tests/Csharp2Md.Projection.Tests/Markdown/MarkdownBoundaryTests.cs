using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Projection.Markdown;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Markdown;

public sealed class MarkdownBoundaryTests
{
    [Fact]
    [Trait("Requirement", "RP-33")]
    public void Project_Pages_DoNotNameAnIdentityAbsentFromTheFacts()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var field = CatalogProjectionFactory.CreateField(dataObject, "order_status");
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            store,
            dataObject,
            field);

        var knownIds = KnownFactIds(view);
        var pages = MarkdownProjectionFactory.Pages(MarkdownProjector.Project(view));
        Assert.NotEmpty(pages);
        foreach (var fragment in pages)
        {
            foreach (var citation in MarkdownProjectionFactory.ParseCitations(MarkdownProjectionFactory.TextOf(fragment)))
            {
                if (citation.Text.StartsWith("id1:", StringComparison.Ordinal))
                {
                    Assert.Contains(citation.Text, knownIds);
                }
            }
        }
    }

    [Fact]
    [Trait("Requirement", "RP-33")]
    public void Project_DoesNotStateAFacetValueAbsentFromThePayload()
    {
        var operation = CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge");
        var view = CatalogProjectionFactory.ViewOf(operation);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), operation.Reference.Id.Value);

        Assert.Contains("inbound", page, StringComparison.Ordinal);
        Assert.DoesNotContain("outbound", page, StringComparison.Ordinal);
        Assert.DoesNotContain("http", page, StringComparison.Ordinal);
        Assert.DoesNotContain("grpc", page, StringComparison.Ordinal);
        foreach (var citation in MarkdownProjectionFactory.ParseCitations(page))
        {
            Assert.Contains(citation.Text, MarkdownProjectionFactory.CitedPayload(view, citation), StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-35")]
    public void Project_DoesNotPublishAPagePerSymbol()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(symbol, component, entry);

        var pages = MarkdownProjectionFactory.Pages(MarkdownProjector.Project(view));

        Assert.DoesNotContain(pages, fragment => fragment.CanonicalKey.Contains("/symbol/", StringComparison.Ordinal));
        Assert.DoesNotContain(
            pages,
            fragment => MarkdownProjectionFactory.TextOf(fragment).Contains(
                "Fact id: [" + symbol.Reference.Id.Value + "]",
                StringComparison.Ordinal));
        Assert.Equal(2, pages.Length);
    }

    [Fact]
    [Trait("Requirement", "RP-35")]
    public void Project_DoesNotPublishAPagePerCallable()
    {
        var symbol = CatalogProjectionFactory.Callable("Charge");
        var view = CatalogProjectionFactory.ViewOf(symbol);

        var fragments = MarkdownProjector.Project(view);

        Assert.Empty(MarkdownProjectionFactory.Pages(fragments));
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.Contains("callable", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-35")]
    public void Project_DoesNotPublishAPagePerObservation()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var view = CatalogProjectionFactory.ViewOf(component);

        var pages = MarkdownProjectionFactory.Pages(MarkdownProjector.Project(view));

        Assert.DoesNotContain(
            pages,
            fragment => fragment.CanonicalKey.Contains("observation", StringComparison.OrdinalIgnoreCase));
        foreach (var kind in Enum.GetNames<ObservationKind>())
        {
            Assert.DoesNotContain(
                pages,
                fragment => fragment.CanonicalKey.Contains(kind, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    [Trait("Requirement", "RP-35")]
    public void Project_DoesNotPublishAPagePerIndividualRelation()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var view = CatalogProjectionFactory.ViewOf(
            [component, unit],
            [MarkdownProjectionFactory.IncludedIn(component, unit)]);

        var pages = MarkdownProjectionFactory.Pages(MarkdownProjector.Project(view));
        var contains = view.Document.ConfirmedRelations["included-in"];

        Assert.Equal(2, pages.Length);
        Assert.NotEmpty(contains);
        Assert.DoesNotContain(pages, fragment => fragment.CanonicalKey.Contains("/relation", StringComparison.Ordinal));
        Assert.DoesNotContain(pages, fragment => fragment.CanonicalKey.Contains("included-in", StringComparison.Ordinal));
        Assert.NotEqual(contains.Length, pages.Length);
    }

    [Fact]
    [Trait("Requirement", "RP-36")]
    public void Project_SameDocumentTwice_ProducesByteIdenticalPages()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var view = CatalogProjectionFactory.ViewOf(
            CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api"),
            CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge"),
            CatalogProjectionFactory.CreateComponent("Orders.Api"),
            CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container"),
            CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced"),
            store,
            dataObject);

        var first = MarkdownProjector.Project(view);
        var second = MarkdownProjector.Project(view);

        Assert.NotEmpty(first);
        Assert.Equal(first.Length, second.Length);
        for (var index = 0; index < first.Length; index++)
        {
            Assert.Equal(first[index].CanonicalKey, second[index].CanonicalKey);
            Assert.True(
                first[index].Payload.AsSpan().SequenceEqual(second[index].Payload.AsSpan()),
                "Canonical payload bytes at '" + first[index].CanonicalKey + "' differ across retries.");
        }
    }

    private static HashSet<string> KnownFactIds(PublishedPackageView view)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var document = view.Document;
        Add(ids, document.Solutions.Select(static dto => dto.Identity.Id));
        Add(ids, document.Projects.Select(static dto => dto.Identity.Id));
        Add(ids, document.Documents.Select(static dto => dto.Identity.Id));
        Add(ids, document.Symbols.Select(static dto => dto.Identity.Id));
        Add(ids, document.Components.Select(static dto => dto.Identity.Id));
        Add(ids, document.DeploymentUnits.Select(static dto => dto.Identity.Id));
        Add(ids, document.EntryPoints.Select(static dto => dto.Identity.Id));
        Add(ids, document.BoundaryOperations.Select(static dto => dto.Identity.Id));
        Add(ids, document.ExternalSystems.Select(static dto => dto.Identity.Id));
        Add(ids, document.Contracts.Select(static dto => dto.Identity.Id));
        Add(ids, document.ContractBindings.Select(static dto => dto.Identity.Id));
        Add(ids, document.ContractRevisions.Select(static dto => dto.Identity.Id));
        Add(ids, document.DataStores.Select(static dto => dto.Identity.Id));
        Add(ids, document.DataObjects.Select(static dto => dto.Identity.Id));
        Add(ids, document.DataFields.Select(static dto => dto.Identity.Id));
        Add(ids, document.DataOperations.Select(static dto => dto.Identity.Id));
        Add(ids, document.ConfigurationBindings.Select(static dto => dto.Identity.Id));
        return ids;
    }

    private static void Add(HashSet<string> ids, IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            ids.Add(value);
        }
    }
}
