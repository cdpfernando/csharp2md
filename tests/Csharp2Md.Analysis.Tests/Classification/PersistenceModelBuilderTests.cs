using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Classification.Persistence;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Tests.Pipeline;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Tests.Classification;

public sealed class PersistenceModelBuilderTests
{
    private const string OrderDbContextFqn = "global::Acme.Orders.Data.OrderDbContext";
    private const string BillingDbContextFqn = "global::Acme.Orders.Data.BillingDbContext";
    private const string OrderFqn = "global::Acme.Orders.Data.Order";
    private const string OrderLineFqn = "global::Acme.Orders.Data.OrderLine";

    [Fact]
    [Trait("Requirement", "PK-10")]
    [Trait("Requirement", "PK-12")]
    public void Build_DbSetPropertyContainer_CreatesOneRelationalStoreNamedAfterTheContextType()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var store = Assert.Single(model.Stores);
        Assert.Equal(OrderDbContextFqn, store.ContextTypeFqn);
        Assert.Equal(DataStoreTechnology.Relational, store.Technology);
        Assert.Equal("Acme.Orders.Data.OrderDbContext", store.Name);
    }

    [Fact]
    [Trait("Requirement", "PK-10")]
    public void Build_ContextTypePayloadWithoutADbSetProperty_StillCreatesTheStore()
    {
        var pipeline = Arrange();
        var writer = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddDataAccess(pipeline, writer, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        var store = Assert.Single(model.Stores);
        Assert.Equal(OrderDbContextFqn, store.ContextTypeFqn);
        Assert.Equal("Acme.Orders.Data.OrderDbContext", store.Name);
    }

    [Fact]
    [Trait("Requirement", "PK-10")]
    public void Build_DbSetPropertyAndContextTypePayloadForOneContext_CreatesOneStore()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        AddDbSetProperty(pipeline, OrderDbContextFqn, "OrderLines", OrderLineFqn);
        var writer = AddMethod(pipeline, "PayOrder", "global::Acme.Orders.Data.OrderWrites");
        AddDataAccess(pipeline, writer, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal([OrderDbContextFqn], model.Stores.Select(store => store.ContextTypeFqn));
    }

    [Fact]
    [Trait("Requirement", "PK-11")]
    public void Build_ConfigurationKeyInACallableWhoseSignatureNamesTheContext_NamesTheStoreAfterTheKey()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var registration = AddMethod(pipeline, "AddOrdersDb", "global::Acme.Orders.Program", OrderDbContextFqn);
        AddConfiguration(pipeline, registration, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal("OrdersDb", Assert.Single(model.Stores).Name);
    }

    [Fact]
    [Trait("Requirement", "PK-11")]
    public void Build_ConfigurationKeyInACallableWhosePayloadNamesTheContext_NamesTheStoreAfterTheKey()
    {
        var pipeline = Arrange();
        var registration = AddMethod(pipeline, "ConfigureHost", "global::Acme.Orders.Program");
        AddDataAccess(pipeline, registration, ordinal: 1, ("operation", "unknown"), ("context-type", OrderDbContextFqn));
        AddConfiguration(pipeline, registration, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal("OrdersDb", Assert.Single(model.Stores).Name);
    }

    [Fact]
    [Trait("Requirement", "PK-12")]
    public void Build_ConfigurationKeyInACallableThatNeverNamesTheContext_FallsBackToTheTypeName()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        var unrelated = AddMethod(pipeline, "ConfigureLogging", "global::Acme.Orders.Program");
        AddConfiguration(pipeline, unrelated, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal("Acme.Orders.Data.OrderDbContext", Assert.Single(model.Stores).Name);
    }

    [Fact]
    [Trait("Requirement", "PK-13")]
    public void Build_LedgerNamingNoContext_CreatesNoStore()
    {
        var pipeline = Arrange();
        var contract = AddMethod(pipeline, "Handle", "global::Acme.Shared.Contracts.OrderPlacedHandler");
        AddConfiguration(pipeline, contract, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Empty(model.Stores);
    }

    [Fact]
    [Trait("Requirement", "PK-10")]
    [Trait("Requirement", "PK-11")]
    [Trait("Requirement", "PK-12")]
    public void Build_TwoContextsInOneSolution_CreatesTwoStoresOrdinalSortedAndNamedIndependently()
    {
        var pipeline = Arrange();
        AddDbSetProperty(pipeline, OrderDbContextFqn, "Orders", OrderFqn);
        AddDbSetProperty(pipeline, BillingDbContextFqn, "Invoices", "global::Acme.Orders.Data.Invoice");
        var registration = AddMethod(pipeline, "AddOrdersDb", "global::Acme.Orders.Program", OrderDbContextFqn);
        AddConfiguration(pipeline, registration, "OrdersDb");

        var model = PersistenceModelBuilder.Build(new ClassifierContext(pipeline), CancellationToken.None);

        Assert.Equal(
            [BillingDbContextFqn, OrderDbContextFqn],
            model.Stores.Select(store => store.ContextTypeFqn));
        Assert.Equal("Acme.Orders.Data.BillingDbContext", model.Stores[0].Name);
        Assert.Equal("OrdersDb", model.Stores[1].Name);
    }

    private static PipelineContext Arrange()
    {
        var pipeline = new PipelineContext(new SwallowingSession(), "alpha.sln");
        pipeline.Accumulator.AddFact(Solution.Create(AcmeSolution));
        pipeline.Accumulator.AddFact(Project.Create(OrdersProject));
        return pipeline;
    }

    private static SolutionId AcmeSolution =>
        SolutionId.Create(WorkspaceIdentity.Create("acme"), "Acme.Orders.slnx");

    private static ProjectId OrdersProject =>
        ProjectId.Create(AcmeSolution, "Acme.Orders/Acme.Orders.csproj");

    private static void AddDbSetProperty(PipelineContext pipeline, string container, string metadata, string entityTypeFqn) =>
        pipeline.Accumulator.AddFact(
            Symbol.Create(
                CanonicalSymbolSignature.Create(
                    "property",
                    container,
                    metadata,
                    0,
                    PersistenceModelBuilder.DbSetTypePrefix + entityTypeFqn + ">"),
                OrdersProject,
                SymbolFacetSet.Create([])));

    private static Symbol AddMethod(
        PipelineContext pipeline,
        string metadata,
        string container,
        string? parameterTypeFqn = null)
    {
        var symbol = Symbol.Create(
            CanonicalSymbolSignature.Create(
                "method",
                container,
                metadata,
                0,
                "global::System.Void",
                parameterTypeFqn is null ? null : [new SymbolParameterSignature(parameterTypeFqn)]),
            OrdersProject,
            SymbolFacetSet.Create([SymbolFacet.Callable]));
        pipeline.Accumulator.AddFact(symbol);
        return symbol;
    }

    private static void AddDataAccess(
        PipelineContext pipeline,
        Symbol owner,
        int ordinal,
        params (string Key, string Value)[] entries) =>
        pipeline.Accumulator.AddObservation(
            Observe(owner, ObservationKind.DataAccess, ordinal, entries));

    private static void AddConfiguration(PipelineContext pipeline, Symbol owner, string key) =>
        pipeline.Accumulator.AddObservation(
            Observe(owner, ObservationKind.Configuration, 1, [("key", key)]));

    private static Observation Observe(
        Symbol owner,
        ObservationKind kind,
        int ordinal,
        (string Key, string Value)[] entries) =>
        Observation.Create(
            owner.Reference,
            kind,
            NormalizedPayload.Create(
                entries.Select(entry =>
                    new PayloadEntry(entry.Key, StructuralLiteral.Create(LiteralRole.ProtocolName, entry.Value, entry.Key)))),
            ordinal,
            new EvidenceLocator(DocumentId.Create("doc"), "Acme.Orders/Data/OrderDbContext.cs", new SourceSpan(1, 1, 1, 8)),
            EvidenceMethod.Semantic,
            new BindingDiagnostic("bound", "bound"),
            DocumentHash.Create(new string('a', 64)),
            new ExtractorVersion(1));
}
