using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection.Labels;
using Csharp2Md.Projection.Tests.Catalogs;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Labels;

public sealed class LabelProjectorTests
{
    [Fact]
    [Trait("Requirement", "GCPC-093")]
    [Trait("Requirement", "GCPC-094")]
    public void For_EntryPoint_EmitsComponentTypeAndMethodLabelsEqualToCitedValues()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entryPoint = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(component, symbol, entryPoint);
        var identity = view.Document.EntryPoints.Single().Identity;

        var labels = LabelProjector.For(identity, view);

        Assert.Equal(3, labels.Length);
        AssertLabel(view, labels, LabelProjector.Component, "Orders.Api");
        AssertSymbolLabel(view, labels, LabelProjector.Type, "global::Acme.Orders.Host", symbol.Reference.Id.Value);
        AssertSymbolLabel(view, labels, LabelProjector.Method, "Run", symbol.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-093")]
    [Trait("Requirement", "GCPC-094")]
    public void For_BoundaryOperation_WithProtocolVerbAndRoute_EmitsAllSixLabelKinds()
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
        var identity = view.Document.BoundaryOperations.Single().Identity;

        var labels = LabelProjector.For(identity, view);

        Assert.Equal(6, labels.Length);
        AssertLabel(view, labels, LabelProjector.Component, "Payments.Api");
        AssertSymbolLabel(view, labels, LabelProjector.Type, "global::Acme.Orders.Host", symbol.Reference.Id.Value);
        AssertSymbolLabel(view, labels, LabelProjector.Method, "Charge", symbol.Reference.Id.Value);
        AssertLabel(view, labels, LabelProjector.Protocol, "http");
        AssertLabel(view, labels, LabelProjector.Verb, "GET");
        AssertLabel(view, labels, LabelProjector.Route, "/orders/{id}");

        Assert.True(view.TryLocate(identity.Id, out var selfCitation));
        var operationLabels = labels.Where(label =>
            label.Kind is LabelProjector.Protocol or LabelProjector.Verb or LabelProjector.Route);
        Assert.All(operationLabels, label =>
        {
            Assert.Equal(selfCitation.ArtifactKey, label.ArtifactKey);
            Assert.Equal(selfCitation.Ordinal, label.Ordinal);
        });
    }

    [Fact]
    [Trait("Requirement", "GCPC-098")]
    public void For_BoundaryOperation_WithoutProtocolVerbOrRoute_OmitsThoseLabelsRatherThanInferring()
    {
        var symbol = CatalogProjectionFactory.Callable("Charge");
        var component = CatalogProjectionFactory.CreateComponent("Payments.Api");
        var operation = BoundaryOperation.Create(
            symbol.Reference,
            component.Reference,
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "queue.charge", "protocolOperationKey"));
        var view = CatalogProjectionFactory.ViewOf(component, symbol, operation);
        var identity = view.Document.BoundaryOperations.Single().Identity;

        var labels = LabelProjector.For(identity, view);

        Assert.DoesNotContain(labels, label => label.Kind == LabelProjector.Protocol);
        Assert.DoesNotContain(labels, label => label.Kind == LabelProjector.Verb);
        Assert.DoesNotContain(labels, label => label.Kind == LabelProjector.Route);
        Assert.Equal(3, labels.Length);
        AssertLabel(view, labels, LabelProjector.Component, "Payments.Api");
        AssertSymbolLabel(view, labels, LabelProjector.Type, "global::Acme.Orders.Host", symbol.Reference.Id.Value);
        AssertSymbolLabel(view, labels, LabelProjector.Method, "Charge", symbol.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-098")]
    public void For_EntryPoint_WhenSymbolFactIsNotProvenInTheView_OmitsTypeAndMethodLabels()
    {
        var symbol = CatalogProjectionFactory.Callable("Run");
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var entryPoint = EntryPoint.Create(symbol.Reference, component.Reference);
        var view = CatalogProjectionFactory.ViewOf(component, entryPoint);
        var identity = view.Document.EntryPoints.Single().Identity;

        var labels = LabelProjector.For(identity, view);

        var label = Assert.Single(labels);
        Assert.Equal(LabelProjector.Component, label.Kind);
        Assert.Equal("Orders.Api", label.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-098")]
    public void For_UnrecognizedFactType_ReturnsNoLabels()
    {
        var component = CatalogProjectionFactory.CreateComponent("Orders.Api");
        var view = CatalogProjectionFactory.ViewOf(component);
        var unmapped = new FactReferenceDto("id1:external-system;solution=x;name=y", "ExternalSystem");

        var labels = LabelProjector.For(unmapped, view);

        Assert.Empty(labels);
    }

    [Fact]
    [Trait("Requirement", "GCPC-098")]
    public void For_ComponentIdentityWithNoMatchingFact_ReturnsNoLabelRatherThanInferring()
    {
        var view = CatalogProjectionFactory.ViewOf();
        var ghost = new FactReferenceDto("id1:component;solution=acme;name=ghost", "Component");

        var labels = LabelProjector.For(ghost, view);

        Assert.Empty(labels);
    }

    [Fact]
    [Trait("Requirement", "GCPC-084")]
    public void For_EntryPoint_WhenSymbolDeclarationIsInsideARedactedSpan_OmitsTypeAndMethodLabels()
    {
        var documentId = DocumentId.Create("doc-secret");
        var hash = DocumentHash.Create(new string('a', 64));
        var secretSpan = new SourceSpan(10, 1, 10, 40);
        var locator = new DeclarationLocator(documentId, "src/Secret.cs", secretSpan, hash);
        var signature = CanonicalSymbolSignature.Create(
            "method", "global::Acme.Orders.Host", "Charge", 0, "global::System.Void");
        var symbol = Symbol.Create(signature, CatalogProjectionFactory.Project, SymbolFacetSet.Create([SymbolFacet.Callable]), locator);
        var component = CatalogProjectionFactory.CreateComponent("Payments.Api");
        var entryPoint = EntryPoint.Create(symbol.Reference, component.Reference);
        var secret = SuspectedSecretEvidence.Create(documentId, secretSpan, hash, RedactedExcerpt.Create("Password=***"));
        var view = PublishedPackageView.From(
            DomainMapper.ToWire(
                new FactualSnapshot(
                    [component, symbol, entryPoint], [], [], [], [], [], suspectedSecrets: [secret]),
                CatalogProjectionFactory.Context));
        var identity = view.Document.EntryPoints.Single().Identity;

        var labels = LabelProjector.For(identity, view);

        Assert.DoesNotContain(labels, label => label.Kind == LabelProjector.Type);
        Assert.DoesNotContain(labels, label => label.Kind == LabelProjector.Method);
        var label = Assert.Single(labels);
        Assert.Equal(LabelProjector.Component, label.Kind);
    }

    [Fact]
    [Trait("Requirement", "GCPC-084")]
    public void For_EntryPoint_WhenSymbolDeclarationIsOutsideTheRedactedSpan_StillEmitsTypeAndMethodLabels()
    {
        var documentId = DocumentId.Create("doc-secret");
        var hash = DocumentHash.Create(new string('a', 64));
        var secretSpan = new SourceSpan(10, 1, 10, 40);
        var declarationSpan = new SourceSpan(1, 1, 1, 10);
        var locator = new DeclarationLocator(documentId, "src/Secret.cs", declarationSpan, hash);
        var signature = CanonicalSymbolSignature.Create(
            "method", "global::Acme.Orders.Host", "Charge", 0, "global::System.Void");
        var symbol = Symbol.Create(signature, CatalogProjectionFactory.Project, SymbolFacetSet.Create([SymbolFacet.Callable]), locator);
        var component = CatalogProjectionFactory.CreateComponent("Payments.Api");
        var entryPoint = EntryPoint.Create(symbol.Reference, component.Reference);
        var secret = SuspectedSecretEvidence.Create(documentId, secretSpan, hash, RedactedExcerpt.Create("Password=***"));
        var view = PublishedPackageView.From(
            DomainMapper.ToWire(
                new FactualSnapshot(
                    [component, symbol, entryPoint], [], [], [], [], [], suspectedSecrets: [secret]),
                CatalogProjectionFactory.Context));
        var identity = view.Document.EntryPoints.Single().Identity;

        var labels = LabelProjector.For(identity, view);

        Assert.Equal(3, labels.Length);
        AssertSymbolLabel(view, labels, LabelProjector.Type, "global::Acme.Orders.Host", symbol.Reference.Id.Value);
        AssertSymbolLabel(view, labels, LabelProjector.Method, "Charge", symbol.Reference.Id.Value);
    }

    [Fact]
    [Trait("Requirement", "GCPC-094")]
    public void For_NonAxisIdentities_EmitNameLabelEqualToCitedValue()
    {
        var unit = CatalogProjectionFactory.CreateDeploymentUnit("Orders.Container");
        var contract = CatalogProjectionFactory.CreateContract("orders.v1.OrderPlaced");
        var store = CatalogProjectionFactory.CreateStore("OrdersDb");
        var dataObject = CatalogProjectionFactory.CreateObject(store, "order_headers");
        var view = CatalogProjectionFactory.ViewOf(unit, contract, store, dataObject);

        AssertLabel(view, LabelProjector.For(view.Document.DeploymentUnits.Single().Identity, view), LabelProjector.Name, "Orders.Container");
        AssertLabel(view, LabelProjector.For(view.Document.Contracts.Single().Identity, view), LabelProjector.Name, "orders.v1.OrderPlaced");
        AssertLabel(view, LabelProjector.For(view.Document.DataStores.Single().Identity, view), LabelProjector.Name, "OrdersDb");
        AssertLabel(view, LabelProjector.For(view.Document.DataObjects.Single().Identity, view), LabelProjector.Name, "order_headers");
    }

    /// <summary>
    /// Finds the single label of <paramref name="kind"/>, asserts its value equals <paramref name="expectedValue"/>,
    /// and proves the citation it carries actually resolves (via <see cref="PublishedPackageView.TryLocate"/> and
    /// the same artifact-key/ordinal lookup the catalog tests use) back to a fact whose own field is that same
    /// value -- i.e. the label is proven, not inferred (GCPC-094).
    /// </summary>
    private static void AssertLabel(
        PublishedPackageView view, ImmutableArray<LabelDto> labels, string kind, string expectedValue)
    {
        var label = Assert.Single(labels, candidate => candidate.Kind == kind);
        Assert.Equal(expectedValue, label.Value);

        var citedFactId = CatalogProjectionFactory.FactIdAt(view, new ArtifactCitation(label.ArtifactKey, label.Ordinal));
        Assert.True(view.TryLocate(citedFactId, out var citation));
        Assert.Equal(label.ArtifactKey, citation.ArtifactKey);
        Assert.Equal(label.Ordinal, citation.Ordinal);

        var cited = CatalogProjectionFactory.CitedElement(view, new ArtifactCitation(label.ArtifactKey, label.Ordinal));
        Assert.Contains(expectedValue, cited.ToJsonString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Same proof as <see cref="AssertLabel"/>, but for a type/method label: the cited fact is the
    /// <c>Symbol</c> identified by <paramref name="expectedSymbolFactId"/>, whose <c>CanonicalSymbolSignature</c>
    /// wire value carries <paramref name="expectedValue"/> percent-encoded inside a composite key rather than
    /// verbatim, so this proves citation resolution and value equality directly against the known symbol
    /// fact instead of scanning the raw cited JSON for a plain substring.
    /// </summary>
    private static void AssertSymbolLabel(
        PublishedPackageView view, ImmutableArray<LabelDto> labels, string kind, string expectedValue, string expectedSymbolFactId)
    {
        var label = Assert.Single(labels, candidate => candidate.Kind == kind);
        Assert.Equal(expectedValue, label.Value);

        Assert.True(view.TryLocate(expectedSymbolFactId, out var expectedCitation));
        Assert.Equal(expectedCitation.ArtifactKey, label.ArtifactKey);
        Assert.Equal(expectedCitation.Ordinal, label.Ordinal);
    }
}
