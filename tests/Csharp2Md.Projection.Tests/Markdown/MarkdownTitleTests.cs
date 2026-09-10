using System.Text.RegularExpressions;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection.Markdown;
using Csharp2Md.Projection.Tests.Catalogs;

namespace Csharp2Md.Projection.Tests.Markdown;

/// <summary>
/// T44: the page heading leads with the identity's proven compact label instead of the fact type alone
/// (GCPC-096), an HTTP boundary operation's heading shows its proven verb and route without ever
/// synthesizing either (GCPC-101, GCPC-102), and the heading stays byte-identical across two runs of the
/// same input, matching the deterministic guarantee every other projection already carries.
/// </summary>
public sealed class MarkdownTitleTests
{
    [Fact]
    [Trait("Requirement", "GCPC-096")]
    [Trait("Requirement", "GCPC-093")]
    public void Project_EntryPointPage_TitleLeadsWithTheMethodAndComponentLabelsNotTheFactTypeAlone()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(component, symbol, entry);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), entry.Reference.Id.Value);
        var title = TitleLine(page);

        var citations = MarkdownProjectionFactory.ParseCitations(title);
        Assert.True(view.TryLocate(symbol.Reference.Id.Value, out var symbolCitation));
        Assert.True(view.TryLocate(component.Reference.Id.Value, out var componentCitation));
        Assert.Contains(
            citations,
            citation => citation.Text == "Run" && citation.ArtifactKey == symbolCitation.ArtifactKey && citation.Ordinal == symbolCitation.Ordinal);
        Assert.Contains(
            citations,
            citation => citation.Text == "Orders.Api" && citation.ArtifactKey == componentCitation.ArtifactKey && citation.Ordinal == componentCitation.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-101")]
    [Trait("Requirement", "GCPC-093")]
    public void Project_BoundaryOperationPage_TitleShowsTheProvenVerbAndRoute()
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

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), operation.Reference.Id.Value);
        var title = TitleLine(page);
        var citations = MarkdownProjectionFactory.ParseCitations(title);

        Assert.True(view.TryLocate(operation.Reference.Id.Value, out var selfCitation));
        Assert.Contains(
            citations,
            citation => citation.Text == "GET" && citation.ArtifactKey == selfCitation.ArtifactKey && citation.Ordinal == selfCitation.Ordinal);
        Assert.Contains(
            citations,
            citation => citation.Text == "/orders/{id}" && citation.ArtifactKey == selfCitation.ArtifactKey && citation.Ordinal == selfCitation.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "GCPC-101")]
    [Trait("Requirement", "GCPC-102")]
    public void Project_BoundaryOperationPage_WithOnlyVerbProven_TitleOmitsRouteRatherThanSynthesizingIt()
    {
        var symbol = CatalogProjectionFactory.Callable("Charge");
        var component = CatalogProjectionFactory.CreateComponent("Payments.Api");
        var operation = BoundaryOperation.Create(
            symbol.Reference,
            component.Reference,
            BoundaryDirection.Inbound,
            protocol: BoundaryProtocol.Http,
            httpMethod: "GET",
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "GET /charge", "protocolOperationKey"));
        var view = CatalogProjectionFactory.ViewOf(component, symbol, operation);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), operation.Reference.Id.Value);
        var title = TitleLine(page);
        var citations = MarkdownProjectionFactory.ParseCitations(title);

        Assert.Equal(3, citations.Length);
        Assert.Contains(citations, citation => citation.Text == "GET");
        Assert.Contains(citations, citation => citation.Text == "Charge");
        Assert.Contains(citations, citation => citation.Text == "Payments.Api");
    }

    [Fact]
    [Trait("Requirement", "GCPC-096")]
    public void Project_AllSevenPageFamilies_NoTitleConsistsSolelyOfFactTypeAndEncodedIdentity()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var opSymbol = CatalogProjectionFactory.Callable("Charge");
        var opComponent = CatalogProjectionFactory.CreateComponent("Payments.Api");
        var operation = BoundaryOperation.Create(
            opSymbol.Reference,
            opComponent.Reference,
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey"));
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var view = CatalogProjectionFactory.ViewOf(
            [symbol, component, entry, opSymbol, opComponent, operation, unit, contract, store, dataObject]);

        var pages = MarkdownProjectionFactory.Pages(MarkdownProjector.Project(view));

        Assert.Equal(8, pages.Length);
        var factTypeOnlyTitle = new Regex(
            @"^# \[(EntryPoint|BoundaryOperation|Component|DeploymentUnit|Contract|DataStore|DataObject)\]\([^)]+\) <!-- \d+ -->$",
            RegexOptions.CultureInvariant);
        foreach (var fragment in pages)
        {
            var title = TitleLine(MarkdownProjectionFactory.TextOf(fragment));
            Assert.False(factTypeOnlyTitle.IsMatch(title), title);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-096")]
    public void Project_EntryPointPage_TitleBytesAreIdenticalAcrossTwoRuns()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(component, symbol, entry);

        var first = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), entry.Reference.Id.Value);
        var second = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), entry.Reference.Id.Value);

        Assert.Equal(TitleLine(first), TitleLine(second), StringComparer.Ordinal);
        Assert.Equal(first, second, StringComparer.Ordinal);
    }

    private static string TitleLine(string page) =>
        page.Split('\n', 2)[0];
}
