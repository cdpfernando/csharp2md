using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Configuration;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class ConfigurationModelBuilderTests
{
    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Build_SingleDocumentKey_BindsToTheGroupingComponent()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        var key = Assert.Single(model.Keys);
        Assert.Equal("Services:PaymentService", key.KeyPath);
        Assert.Equal(KeyResolution.Literal, key.Resolution);
        Assert.Equal("https://payments.internal.acme.local:8443", key.Address);
        Assert.Equal(
            Component.Create(SolutionId, "Acme.Orders/Acme.Orders.csproj", OwnersOf(pipeline, project)).Reference,
            key.OwningComponent);
        Assert.Equal(document.Reference, key.Evidence.Owner);
        Assert.Equal(1, model.Coverage.KeysDeclared);
        var componentEdge = Assert.Single(model.Edges, edge => edge.Source.Equals(key.OwningComponent));
        Assert.Equal("Services:PaymentService", componentEdge.KeyPath);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Build_TwoDocumentsDeclaringTheSameKey_ProducesTwoEntriesWithoutMerge()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var production = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var development = AddDocument(pipeline, project, "Acme.Orders/appsettings.Development.json");
        Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, production, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);
        AddDeclaredKey(pipeline, development, "Services:PaymentService", "literal", "https://payments.dev.acme.local:8443", ordinal: 1);

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Equal(2, model.Keys.Length);
        Assert.All(model.Keys, key => Assert.Equal("Services:PaymentService", key.KeyPath));
        Assert.Equal(
            new[] { production.Reference, development.Reference }.OrderBy(owner => owner.Id.Value, StringComparer.Ordinal),
            model.Keys.Select(key => key.Evidence.Owner));
        Assert.Equal(2, model.Coverage.KeysDeclared);
        Assert.Equal(2, model.Keys.Select(key => key.Evidence).Distinct().Count());
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Build_DocumentWhoseProjectHasNoComponent_SkipsWithDiagnostic()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Broken/Acme.Broken.csproj");
        var document = AddDocument(pipeline, project, "Acme.Broken/appsettings.json");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", address: null, ordinal: 1);

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Empty(model.Keys);
        Assert.Equal(0, model.Coverage.KeysDeclared);
        var diagnostic = Assert.Single(pipeline.Accumulator.ToSnapshot().Diagnostics.ToArray());
        Assert.Equal(ConfigurationModelBuilder.UngroupedDocumentCode, diagnostic.Code);
        Assert.Equal(document.RelativePath, diagnostic.IdentityOrKey);
        Assert.Contains(document.RelativePath, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "CDC-35")]
    public void Build_ProjectOwnedAndSymbolOwnedConfiguration_AreIgnored()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        pipeline.Accumulator.AddObservation(
            Observe(project.Reference, 1, ("output-kind", "application")));
        var symbol = OwnersOf(pipeline, project)[0];
        pipeline.Accumulator.AddObservation(
            Observe(symbol, 1, ("key", "Logging:Level")));

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Empty(model.Keys);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Diagnostics);
    }

    [Fact]
    [Trait("Requirement", "CDC-36")]
    [Trait("Requirement", "CDC-37")]
    [Trait("Requirement", "CDC-38")]
    [Trait("Requirement", "CDC-39")]
    [Trait("Requirement", "CDC-40")]
    public void Build_RegisteredTriples_ProduceComponentSymbolStoreAndBoundaryEdges()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var component = Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);
        AddDeclaredKey(pipeline, document, "ConnectionStrings:OrdersDb", "literal", address: null, ordinal: 2);
        var reader = OwnersOf(pipeline, project)[0];
        pipeline.Accumulator.AddObservation(Observe(reader, 1, ("key", "Services:PaymentService")));
        var store = DataStore.Create(
            DataStoreTechnology.Relational,
            StructuralLiteral.Create(LiteralRole.ClientName, "OrdersDb", "name"));
        pipeline.Accumulator.AddFact(store);
        var callable = AddCallable(pipeline, project, "PlaceOrderAsync", "global::Acme.Orders.OrderService");
        var operation = BoundaryOperation.Create(
            callable.Reference,
            component.Reference,
            BoundaryDirection.Outbound,
            BoundaryProtocol.Http,
            "PaymentService",
            "POST",
            StructuralLiteral.Create(LiteralRole.Route, "payments/authorize", "route"));
        pipeline.Accumulator.AddFact(operation);

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Contains(model.Edges, edge => edge.Source.Equals(component.Reference) && edge.KeyPath == "Services:PaymentService");
        Assert.Contains(model.Edges, edge => edge.Source.Equals(component.Reference) && edge.KeyPath == "ConnectionStrings:OrdersDb");
        var symbolEdge = Assert.Single(model.Edges, edge => edge.Source.Equals(reader) && edge.KeyPath == "Services:PaymentService");
        Assert.Contains(symbolEdge.Evidence.DerivedFrom, identity => identity.Owner.Equals(reader));
        Assert.Contains(symbolEdge.Evidence.DerivedFrom, identity => identity.Owner.Equals(document.Reference));
        Assert.Contains(model.Edges, edge => edge.Source.Equals(store.Reference) && edge.KeyPath == "ConnectionStrings:OrdersDb");
        var boundaryEdge = Assert.Single(model.Edges, edge => edge.Source.Equals(operation.Reference) && edge.KeyPath == "Services:PaymentService");
        Assert.Contains(boundaryEdge.Evidence.DerivedFrom, identity => identity.Owner.Equals(document.Reference));
        Assert.Empty(model.UnboundReads);
    }

    [Fact]
    [Trait("Requirement", "CDC-41")]
    public void Build_PrefixSuffixAndCaseVariants_ProduceNoConsumerEdge()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var component = Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);
        AddDeclaredKey(pipeline, document, "ConnectionStrings:OrdersDb", "literal", address: null, ordinal: 2);
        var prefix = AddCallable(pipeline, project, "PrefixClient", "global::Acme.Orders.Clients");
        var suffix = AddCallable(pipeline, project, "SuffixClient", "global::Acme.Orders.Clients");
        var folded = AddCallable(pipeline, project, "FoldedClient", "global::Acme.Orders.Clients");
        pipeline.Accumulator.AddFact(
            BoundaryOperation.Create(
                prefix.Reference,
                component.Reference,
                BoundaryDirection.Outbound,
                BoundaryProtocol.Http,
                "Payment",
                "POST",
                StructuralLiteral.Create(LiteralRole.Route, "payments/authorize", "route")));
        pipeline.Accumulator.AddFact(
            BoundaryOperation.Create(
                suffix.Reference,
                component.Reference,
                BoundaryDirection.Outbound,
                BoundaryProtocol.Http,
                "PaymentServiceClient",
                "POST",
                StructuralLiteral.Create(LiteralRole.Route, "payments/authorize", "route")));
        pipeline.Accumulator.AddFact(
            BoundaryOperation.Create(
                folded.Reference,
                component.Reference,
                BoundaryDirection.Outbound,
                BoundaryProtocol.Http,
                "paymentservice",
                "POST",
                StructuralLiteral.Create(LiteralRole.Route, "payments/authorize", "route")));
        pipeline.Accumulator.AddFact(
            DataStore.Create(
                DataStoreTechnology.Relational,
                StructuralLiteral.Create(LiteralRole.ClientName, "ordersdb", "name")));
        pipeline.Accumulator.AddObservation(Observe(OwnersOf(pipeline, project)[0], 1, ("key", "Services:Payment")));

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.DoesNotContain(
            model.Edges,
            edge => edge.Source.FactType is nameof(BoundaryOperation) or nameof(DataStore) or nameof(Symbol));
        Assert.Single(model.UnboundReads);
    }

    [Fact]
    [Trait("Requirement", "CDC-42")]
    public void Build_SymbolReadKeyDeclaredNowhere_ProducesUnboundReadNotAnEdge()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        var reader = OwnersOf(pipeline, project)[0];
        pipeline.Accumulator.AddObservation(Observe(reader, 1, ("key", "FeatureManagement:Missing")));

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        var unbound = Assert.Single(model.UnboundReads);
        Assert.Equal(reader, unbound.Symbol);
        Assert.DoesNotContain(model.Edges, edge => edge.Source.Equals(reader));
        Assert.Empty(model.Keys);
    }

    [Fact]
    [Trait("Requirement", "CDC-38")]
    public void Build_DataStoreNameMatchingNoConnectionString_ProducesNoEdgeAndNoUnresolved()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "ConnectionStrings:OrdersDb", "literal", address: null, ordinal: 1);
        var store = DataStore.Create(
            DataStoreTechnology.Relational,
            StructuralLiteral.Create(LiteralRole.ClientName, "BillingDb", "name"));
        pipeline.Accumulator.AddFact(store);

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.DoesNotContain(model.Edges, edge => edge.Source.Equals(store.Reference));
        Assert.Empty(model.UnboundReads);
        Assert.Empty(pipeline.Accumulator.ToSnapshot().Unresolved);
    }

    [Fact]
    [Trait("Requirement", "CDC-43")]
    [Trait("Requirement", "CDC-46")]
    [Trait("Requirement", "CDC-47")]
    public void Build_LiteralServicesKeyWithAddress_PromotesCandidateWithBothObservations()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var component = Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);
        var (operation, external, csharp) = AddTargetsCandidate(pipeline, project, component, "PaymentService");
        var externalsBefore = pipeline.Accumulator.ToSnapshot().Facts.OfType<ExternalSystem>().Count();

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        var decision = Assert.Single(model.Targets);
        Assert.Equal(TargetOutcome.Promote, decision.Outcome);
        Assert.Equal(operation.Reference, decision.Candidate.Source);
        Assert.Equal(external.Reference, decision.Candidate.ProposedTarget);
        Assert.Contains(decision.Evidence.DerivedFrom, identity => identity.Equals(csharp.Identity));
        Assert.Contains(decision.Evidence.DerivedFrom, identity => identity.Owner.Equals(document.Reference));
        Assert.Equal(externalsBefore, pipeline.Accumulator.ToSnapshot().Facts.OfType<ExternalSystem>().Count());
        Assert.Equal(1, externalsBefore);
    }

    [Fact]
    [Trait("Requirement", "CDC-44")]
    public void Build_DynamicServicesKey_YieldsFrontier()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var component = Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:NotificationService", "dynamic", address: null, ordinal: 1);
        AddTargetsCandidate(pipeline, project, component, "NotificationService");

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        var decision = Assert.Single(model.Targets);
        Assert.Equal(TargetOutcome.Frontier, decision.Outcome);
        Assert.Contains(decision.Evidence.DerivedFrom, identity => identity.Owner.Equals(document.Reference));
    }

    [Fact]
    [Trait("Requirement", "CDC-45")]
    public void Build_NoMatchingServicesKey_YieldsLeaveWithNoFrontier()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var component = Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);
        AddTargetsCandidate(pipeline, project, component, "ShippingService");

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        var decision = Assert.Single(model.Targets);
        Assert.Equal(TargetOutcome.Leave, decision.Outcome);
    }

    [Fact]
    [Trait("Requirement", "CDC-43")]
    public void Build_LiteralServicesKeyWithoutAddress_YieldsLeaveNotPromote()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var component = Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", address: null, ordinal: 1);
        AddTargetsCandidate(pipeline, project, component, "PaymentService");

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        var decision = Assert.Single(model.Targets);
        Assert.Equal(TargetOutcome.Leave, decision.Outcome);
        Assert.NotEqual(TargetOutcome.Promote, decision.Outcome);
    }

    [Fact]
    [Trait("Requirement", "CDC-48")]
    public void Build_PrefixOrCaseVariantClientName_YieldsLeave()
    {
        var pipeline = Arrange();
        var project = AddProject(pipeline, "Acme.Orders/Acme.Orders.csproj");
        var document = AddDocument(pipeline, project, "Acme.Orders/appsettings.json");
        var component = Group(pipeline, project, "Acme.Orders/Acme.Orders.csproj");
        AddDeclaredKey(pipeline, document, "Services:PaymentService", "literal", "https://payments.internal.acme.local:8443", ordinal: 1);
        AddTargetsCandidate(pipeline, project, component, "Payment");
        AddTargetsCandidate(pipeline, project, component, "paymentservice");

        var model = ConfigurationModelBuilder.Build(new ClassifierContext(pipeline));

        Assert.Equal(2, model.Targets.Length);
        Assert.All(model.Targets, decision => Assert.Equal(TargetOutcome.Leave, decision.Outcome));
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.Accumulator.AddFact(Solution.Create(SolutionId));
        return pipeline;
    }

    private static SolutionId SolutionId =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static Project AddProject(PipelineContext pipeline, string logicalPath)
    {
        var project = Project.Create(ProjectId.Create(SolutionId, logicalPath));
        pipeline.Accumulator.AddFact(project);
        return project;
    }

    private static Document AddDocument(PipelineContext pipeline, Project project, string relativePath)
    {
        var document = Document.Create(project.Id, relativePath);
        pipeline.Accumulator.AddFact(document);
        return document;
    }

    private static Component Group(PipelineContext pipeline, Project project, string componentName)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", "global::Acme.Orders.Program", "Main", 0, "global::System.Void"),
            project.Id,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        var component = Component.Create(SolutionId, componentName, [symbol.Reference]);
        pipeline.Accumulator.AddFact(component);
        return component;
    }

    private static Symbol AddCallable(PipelineContext pipeline, Project project, string metadata, string container)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create("method", container, metadata, 0, "global::System.Threading.Tasks.Task"),
            project.Id,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static (BoundaryOperation Operation, ExternalSystem External, Observation Csharp) AddTargetsCandidate(
        PipelineContext pipeline,
        Project project,
        Component component,
        string clientName)
    {
        var callable = AddCallable(pipeline, project, clientName + "Call", "global::Acme.Orders.Clients");
        var operation = BoundaryOperation.Create(
            callable.Reference,
            component.Reference,
            BoundaryDirection.Outbound,
            BoundaryProtocol.Http,
            clientName,
            "POST",
            StructuralLiteral.Create(LiteralRole.Route, "payments/authorize", "route"));
        pipeline.Accumulator.AddFact(operation);
        var external = ExternalSystem.Create(
            SolutionId,
            StructuralLiteral.Create(LiteralRole.ClientName, clientName, "client-name"));
        pipeline.Accumulator.AddFact(external);
        var csharp = Observation.Create(
            callable.Reference,
            ObservationKind.Invocation,
            NormalizedPayload.Create(
            [
                new PayloadEntry(
                    "client-name",
                    StructuralLiteral.Create(LiteralRole.ClientName, clientName, "client-name")),
            ]),
            1,
            new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/OrderService.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
        pipeline.Accumulator.AddObservation(csharp);
        pipeline.Accumulator.AddCandidate(
            CandidateLink.Create(
                RelationKind.Targets,
                operation.Reference,
                external.Reference,
                EvidenceChain.Create([csharp.Identity])));
        return (operation, external, csharp);
    }

    private static FactReference[] OwnersOf(PipelineContext pipeline, Project project) =>
        pipeline.Accumulator.ToSnapshot().Facts.OfType<Symbol>()
            .Where(symbol => symbol.OwningProject.Equals(project.Id))
            .Select(symbol => symbol.Reference)
            .ToArray();

    private static void AddDeclaredKey(
        PipelineContext pipeline,
        Document document,
        string key,
        string resolution,
        string? address,
        int ordinal)
    {
        var entries = new List<(string Key, string Value)>
        {
            ("key", key),
            ("resolution", resolution),
        };
        if (address is not null)
        {
            entries.Add(("address", address));
        }

        pipeline.Accumulator.AddObservation(Observe(document.Reference, ordinal, [.. entries]));
    }

    private static Observation Observe(FactReference owner, int ordinal, params (string Key, string Value)[] entries) =>
        Observation.Create(
            owner,
            ObservationKind.Configuration,
            NormalizedPayload.Create(
                entries.Select(entry =>
                    new PayloadEntry(
                        entry.Key,
                        StructuralLiteral.Create(LiteralRole.ConfigurationKey, entry.Value, entry.Key)))),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/appsettings.json", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Configured,
            new BindingDiagnostic("configured", "configured"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
