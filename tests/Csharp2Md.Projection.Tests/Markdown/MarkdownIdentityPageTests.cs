using Csharp2Md.Domain.Facts;
using Csharp2Md.Projection.Markdown;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Markdown;

public sealed class MarkdownIdentityPageTests
{
    [Fact]
    [Trait("Requirement", "RP-31")]
    [Trait("Requirement", "RP-34")]
    public void Project_ComponentPage_StatesFactId()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var view = CatalogProjectionFactory.ViewOf(component);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), component.Reference.Id.Value);

        Assert.Contains(component.Reference.Id.Value, page, StringComparison.Ordinal);
        Assert.Contains(MarkdownProjectionFactory.ParseCitations(page), citation => citation.Text == component.Reference.Id.Value);
        Assert.True(view.TryLocate(component.Reference.Id.Value, out var located));
        Assert.Contains(
            MarkdownProjectionFactory.ParseCitations(page),
            citation => citation.Text == component.Reference.Id.Value
                && citation.ArtifactKey == located.ArtifactKey
                && citation.Ordinal == located.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    [Trait("Requirement", "RP-34")]
    public void Project_DeploymentUnitPage_StatesFactId()
    {
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var view = CatalogProjectionFactory.ViewOf(unit);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), unit.Reference.Id.Value);

        Assert.Contains(unit.Reference.Id.Value, page, StringComparison.Ordinal);
        Assert.Contains(MarkdownProjectionFactory.ParseCitations(page), citation => citation.Text == unit.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    [Trait("Requirement", "RP-34")]
    public void Project_ContractPage_StatesFactId()
    {
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var view = CatalogProjectionFactory.ViewOf(contract);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), contract.Reference.Id.Value);

        Assert.Contains(contract.Reference.Id.Value, page, StringComparison.Ordinal);
        Assert.Contains(MarkdownProjectionFactory.ParseCitations(page), citation => citation.Text == contract.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    [Trait("Requirement", "RP-34")]
    public void Project_DataStorePage_CitesTechnologyFromPersistencePayload()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var view = CatalogProjectionFactory.ViewOf(store);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), store.Reference.Id.Value);
        var citations = MarkdownProjectionFactory.ParseCitations(page);

        Assert.Contains(citations, citation => citation.Text == "relational");
        Assert.Contains(citations, citation => citation.Text == "technology");
        Assert.True(view.TryLocate(store.Reference.Id.Value, out var located));
        Assert.Contains(
            citations,
            citation => citation.Text == "relational"
                && citation.ArtifactKey == located.ArtifactKey
                && citation.Ordinal == located.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    [Trait("Requirement", "RP-34")]
    public void Project_DataObjectPage_CitesFormAndMappingStateFromPersistencePayload()
    {
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var view = CatalogProjectionFactory.ViewOf(store, dataObject);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), dataObject.Reference.Id.Value);
        var citations = MarkdownProjectionFactory.ParseCitations(page);

        Assert.Contains(citations, citation => citation.Text == "table");
        Assert.Contains(citations, citation => citation.Text == "form");
        Assert.Contains(citations, citation => citation.Text == "mapping_state");
        Assert.Contains(citations, citation => citation.Text == "explicit-confirmation");
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    public void Project_EmitsOnePagePerComponentContractAndPersistenceIdentity()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var view = CatalogProjectionFactory.ViewOf(component, unit, contract, store, dataObject);

        var pages = MarkdownProjectionFactory.Pages(MarkdownProjector.Project(view));

        Assert.Equal(5, pages.Length);
        Assert.Contains(pages, fragment => fragment.CanonicalKey.StartsWith("markdown/component/", StringComparison.Ordinal));
        Assert.Contains(pages, fragment => fragment.CanonicalKey.StartsWith("markdown/deployment-unit/", StringComparison.Ordinal));
        Assert.Contains(pages, fragment => fragment.CanonicalKey.StartsWith("markdown/contract/", StringComparison.Ordinal));
        Assert.Contains(pages, fragment => fragment.CanonicalKey.StartsWith("markdown/data-store/", StringComparison.Ordinal));
        Assert.Contains(pages, fragment => fragment.CanonicalKey.StartsWith("markdown/data-object/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    [Trait("Requirement", "RP-34")]
    public void Project_IncludedInRelation_IsCitedOnComponentAndDeploymentUnitPages()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var view = CatalogProjectionFactory.ViewOf(
            [component, unit],
            [MarkdownProjectionFactory.IncludedIn(component, unit)]);

        var componentPage = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), component.Reference.Id.Value);
        var unitPage = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), unit.Reference.Id.Value);

        Assert.Contains("included-in", componentPage, StringComparison.Ordinal);
        Assert.Contains("included-in", unitPage, StringComparison.Ordinal);
        Assert.Contains(MarkdownProjectionFactory.ParseCitations(componentPage), citation => citation.Text == "included-in");
        Assert.Contains(MarkdownProjectionFactory.ParseCitations(unitPage), citation => citation.Text == "included-in");
    }

    [Fact]
    [Trait("Requirement", "RP-34")]
    public void Project_EveryReproducedValue_IsPresentAtCitedArtifactOrdinal()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var symbol = CatalogProjectionFactory.Callable("Run");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var operation = CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge");
        var view = CatalogProjectionFactory.ViewOf(
            [symbol, component, unit, contract, store, dataObject, entry, operation],
            [
                MarkdownProjectionFactory.Executes(entry, symbol),
                MarkdownProjectionFactory.IncludedIn(component, unit),
            ]);

        var pages = MarkdownProjectionFactory.Pages(MarkdownProjector.Project(view));
        Assert.Equal(7, pages.Length);
        foreach (var fragment in pages)
        {
            var page = MarkdownProjectionFactory.TextOf(fragment);
            var citations = MarkdownProjectionFactory.ParseCitations(page);
            Assert.NotEmpty(citations);
            foreach (var citation in citations)
            {
                Assert.False(string.IsNullOrEmpty(citation.ArtifactKey), page);
                Assert.True(citation.Ordinal >= 0, citation.ArtifactKey);
                var payload = MarkdownProjectionFactory.CitedPayload(view, citation);
                Assert.Contains(citation.Text, payload, StringComparison.Ordinal);
            }
        }
    }
}
