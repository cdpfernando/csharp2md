using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection.Catalogs;
using Csharp2Md.Projection.Markdown;
using Csharp2Md.Projection.Postings;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Projection.Tests.Source;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Markdown;

public sealed class MarkdownProjectorTests
{
    [Fact]
    [Trait("Requirement", "RP-31")]
    [Trait("Requirement", "RP-32")]
    public void Project_EntryPointPage_StatesFactIdFacetValuesAndDirectConfirmedRelations()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(
            [symbol, component, entry],
            [MarkdownProjectionFactory.Executes(entry, symbol)]);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), entry.Reference.Id.Value);

        Assert.Contains(entry.Reference.Id.Value, page, StringComparison.Ordinal);
        Assert.Contains("## Facets", page, StringComparison.Ordinal);
        Assert.Contains("executes", page, StringComparison.Ordinal);
        Assert.Contains(symbol.Reference.Id.Value, page, StringComparison.Ordinal);
        var citations = MarkdownProjectionFactory.ParseCitations(page);
        Assert.Contains(citations, citation => citation.Text == entry.Reference.Id.Value);
        Assert.Contains(citations, citation => citation.Text == "executes");
        Assert.Contains(citations, citation => citation.Text == symbol.Reference.Id.Value);
        Assert.True(view.TryLocate(entry.Reference.Id.Value, out var located));
        Assert.Contains(
            citations,
            citation => citation.Text == entry.Reference.Id.Value
                && citation.ArtifactKey == located.ArtifactKey
                && citation.Ordinal == located.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    [Trait("Requirement", "RP-32")]
    public void Project_BoundaryOperationPage_StatesFactIdFacetValuesAndDirectConfirmedRelations()
    {
        var symbol = CatalogProjectionFactory.Callable("Charge");
        var component = CatalogProjectionFactory.CreateComponent("Payments.Api");
        var operation = BoundaryOperation.Create(
            symbol.Reference,
            component.Reference,
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey"));
        var view = CatalogProjectionFactory.ViewOf(
            [symbol, component, operation],
            [MarkdownProjectionFactory.ImplementsOperation(symbol, operation)]);

        var page = MarkdownProjectionFactory.PageFor(MarkdownProjector.Project(view), operation.Reference.Id.Value);

        Assert.Contains(operation.Reference.Id.Value, page, StringComparison.Ordinal);
        Assert.Contains("inbound", page, StringComparison.Ordinal);
        Assert.Contains("implements-operation", page, StringComparison.Ordinal);
        var citations = MarkdownProjectionFactory.ParseCitations(page);
        Assert.Contains(citations, citation => citation.Text == "inbound");
        Assert.Contains(citations, citation => citation.Text == "direction");
        Assert.Contains(citations, citation => citation.Text == "implements-operation");
        Assert.True(view.TryLocate(operation.Reference.Id.Value, out var located));
        Assert.Contains(
            citations,
            citation => citation.Text == operation.Reference.Id.Value
                && citation.ArtifactKey == located.ArtifactKey
                && citation.Ordinal == located.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-32")]
    public void Project_Pages_LinkToCatalogsPostingsAndSourceThatExistInTheSamePublication()
    {
        var (sourceView, reader, documents) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", "class Program;"u8.ToArray()));
        var document = Assert.Single(documents);
        var symbol = Symbol.Create(
            CatalogProjectionFactory.Callable("Run").Signature,
            CatalogProjectionFactory.Project,
            SymbolFacetSet.Create([SymbolFacet.Callable]),
            new DeclarationLocator(
                DocumentId.Create(document.Reference.Id.Value),
                document.RelativePath,
                new SourceSpan(1, 1, 1, 15),
                document.SourceHash ?? DocumentHash.Create(new string('a', 64))));
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var operation = CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge");
        var architecture = CatalogProjectionFactory.ViewOf(
            [symbol, component, entry, operation],
            [MarkdownProjectionFactory.Executes(entry, symbol)]);
        var view = PublishedPackageView.From(
            sourceView.Document with
            {
                Symbols = architecture.Document.Symbols,
                Components = architecture.Document.Components,
                EntryPoints = architecture.Document.EntryPoints,
                BoundaryOperations = architecture.Document.BoundaryOperations,
                ConfirmedRelations = architecture.Document.ConfirmedRelations,
            });

        var fragments = MarkdownProjector.Project(view);
        var keys = MarkdownProjectionFactory.PublicationKeys(view, reader);
        var page = MarkdownProjectionFactory.PageFor(fragments, entry.Reference.Id.Value);
        var citations = MarkdownProjectionFactory.ParseCitations(page);

        Assert.Contains(citations, citation => citation.ArtifactKey.StartsWith("catalogs/", StringComparison.Ordinal));
        Assert.Contains(citations, citation => citation.ArtifactKey.StartsWith("postings/", StringComparison.Ordinal));
        Assert.Contains(citations, citation => citation.ArtifactKey.StartsWith("source/", StringComparison.Ordinal));
        Assert.All(
            citations,
            citation => Assert.True(
                keys.Contains(citation.ArtifactKey),
                citation.ArtifactKey + " is not in the same publication"));
        var composed = new PackageProjector().Project(view, reader);
        Assert.All(
            citations.Where(static citation =>
                citation.ArtifactKey.StartsWith("catalogs/", StringComparison.Ordinal)
                || citation.ArtifactKey.StartsWith("postings/", StringComparison.Ordinal)
                || citation.ArtifactKey.StartsWith("source/", StringComparison.Ordinal)),
            citation => Assert.Contains(composed, fragment => fragment.CanonicalKey == citation.ArtifactKey));
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    public void Project_EmitsOnePagePerEntryPointAndPerBoundaryOperation()
    {
        var first = CatalogProjectionFactory.CreateEntryPoint("Run", "Orders.Api");
        var second = CatalogProjectionFactory.CreateEntryPoint("Main", "Orders.Worker");
        var charge = CatalogProjectionFactory.CreateInboundOperation("Charge", "Payments.Api", "POST /charge");
        var refund = CatalogProjectionFactory.CreateInboundOperation("Refund", "Payments.Api", "POST /refund");
        var view = CatalogProjectionFactory.ViewOf(first, second, charge, refund);

        var pages = MarkdownProjectionFactory.Pages(MarkdownProjector.Project(view));

        Assert.Equal(4, pages.Length);
        Assert.Contains(pages, fragment => MarkdownProjectionFactory.TextOf(fragment).Contains(first.Reference.Id.Value, StringComparison.Ordinal));
        Assert.Contains(pages, fragment => MarkdownProjectionFactory.TextOf(fragment).Contains(second.Reference.Id.Value, StringComparison.Ordinal));
        Assert.Contains(pages, fragment => MarkdownProjectionFactory.TextOf(fragment).Contains(charge.Reference.Id.Value, StringComparison.Ordinal));
        Assert.Contains(pages, fragment => MarkdownProjectionFactory.TextOf(fragment).Contains(refund.Reference.Id.Value, StringComparison.Ordinal));
        Assert.Equal(4, pages.Select(static fragment => fragment.CanonicalKey).Distinct(StringComparer.Ordinal).Count());
        Assert.All(pages.Take(2), static fragment => Assert.StartsWith("markdown/entry-point/", fragment.CanonicalKey, StringComparison.Ordinal));
        Assert.All(pages.Skip(2), static fragment => Assert.StartsWith("markdown/boundary-operation/", fragment.CanonicalKey, StringComparison.Ordinal));
        Assert.All(pages, static fragment => Assert.EndsWith(".md", fragment.CanonicalKey, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    public void Project_NoEntryPointsOrBoundaryOperations_OmitsMarkdownPages()
    {
        var view = CatalogProjectionFactory.ViewOf(CatalogProjectionFactory.CreateSolutionFact());

        var fragments = MarkdownProjector.Project(view);

        Assert.Empty(fragments);
        Assert.DoesNotContain(fragments, fragment => fragment.CanonicalKey.StartsWith("markdown/", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-31")]
    public void PackageProjector_ComposesMarkdownProjectorAfterPostings()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entry = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(
            [symbol, component, entry],
            [MarkdownProjectionFactory.Executes(entry, symbol)]);
        var reader = new EmptySourceReader();

        var composed = new PackageProjector().Project(view, reader);
        var catalogs = CatalogProjector.Project(view);
        var postings = PostingProjector.Project(view);
        var pages = MarkdownProjector.Project(view);

        Assert.Equal(
            catalogs.Select(static fragment => fragment.CanonicalKey)
                .Concat(postings.Select(static fragment => fragment.CanonicalKey))
                .Concat(pages.Select(static fragment => fragment.CanonicalKey)),
            composed.Select(static fragment => fragment.CanonicalKey).Take(catalogs.Length + postings.Length + pages.Length));
        Assert.Equal(pages[^1].CanonicalKey, composed[catalogs.Length + postings.Length + pages.Length - 1].CanonicalKey);
        Assert.StartsWith("markdown/", composed[catalogs.Length + postings.Length + pages.Length - 1].CanonicalKey, StringComparison.Ordinal);
    }
}
