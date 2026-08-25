using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class RelationShapeGuardsTests
{
    private static readonly TaxonomyRegistry Registry = new(TaxonomyTables.Default);

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");

    private static FactReference ComponentReference(string name) =>
        new(new FactId("component", $"id1:component;name={name}"), "Component");

    private static Symbol MethodSymbol(string metadataName, params SymbolFacet[] facets)
    {
        var signature = CanonicalSymbolSignature.Create("method", "global::Acme.Payment", metadataName, 0, "global::System.Void");
        return Symbol.Create(signature, AcmeProject, SymbolFacetSet.Create(facets));
    }

    public static IEnumerable<object[]> CallableRequiringRelations()
    {
        yield return [RelationKind.Executes];
        yield return [RelationKind.Invokes];
        yield return [RelationKind.ImplementsOperation];
        yield return [RelationKind.AccessesData];
    }

    [Theory]
    [MemberData(nameof(CallableRequiringRelations))]
    [Trait("Requirement", "TAX-46")]
    public void RequireCallableIfNeeded_SymbolWithoutCallableFacet_IsRejectedNamingTheRelation(RelationKind kind)
    {
        var symbol = MethodSymbol("Charge");

        var exception = Assert.Throws<ArgumentException>(
            () => RelationShapeGuards.RequireCallableIfNeeded(kind, symbol, "source"));

        Assert.Equal("source", exception.ParamName);
        Assert.Contains(kind.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(CallableRequiringRelations))]
    [Trait("Requirement", "TAX-46")]
    public void RequireCallableIfNeeded_SymbolWithCallableFacet_DoesNotThrow(RelationKind kind)
    {
        var symbol = MethodSymbol("Charge", SymbolFacet.Callable);

        RelationShapeGuards.RequireCallableIfNeeded(kind, symbol, "source");
    }

    [Fact]
    [Trait("Requirement", "TAX-46")]
    public void RequireCallableIfNeeded_RelationThatDoesNotRequireACallable_DoesNotThrowForANonCallableSymbol()
    {
        var symbol = MethodSymbol("Charge");

        RelationShapeGuards.RequireCallableIfNeeded(RelationKind.BelongsTo, symbol, "source");
    }

    [Fact]
    [Trait("Requirement", "TAX-49")]
    public void RequirePayloadRoleForUsesContract_MissingPayloadRole_IsRejectedNamingRelationAndFacets()
    {
        var facets = FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

        var exception = Assert.Throws<ArgumentException>(
            () => RelationShapeGuards.RequirePayloadRoleForUsesContract(RelationKind.UsesContract, facets));

        Assert.Equal("facets", exception.ParamName);
        Assert.Contains(nameof(RelationKind.UsesContract), exception.Message, StringComparison.Ordinal);
        Assert.Contains("payload-role", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-49")]
    public void RequirePayloadRoleForUsesContract_RegisteredPayloadRole_DoesNotThrow()
    {
        var registeredAxes = TaxonomyTables.Default.FacetAxes.Add(new FacetAxisDescriptor("payload-role", PayloadRoleTable.All));
        var facets = FacetBinding.Create(
            registeredAxes, ["payload-role"], [new FacetBindingEntry("payload-role", PayloadRoleTable.All[0])]);

        RelationShapeGuards.RequirePayloadRoleForUsesContract(RelationKind.UsesContract, facets);
    }

    [Fact]
    [Trait("Requirement", "TAX-49")]
    public void RequirePayloadRoleForUsesContract_RelationOtherThanUsesContract_DoesNotThrowEvenWithoutARole()
    {
        var facets = FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);

        RelationShapeGuards.RequirePayloadRoleForUsesContract(RelationKind.AccessesData, facets);
    }

    [Fact]
    [Trait("Requirement", "TAX-50")]
    public void RequireLegalTargetShape_Targets_AcceptsAnInboundBoundaryOperation()
    {
        var operation = BoundaryOperation.Create(
            MethodSymbol("Charge").Reference,
            ComponentReference("payments-api"),
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey"));

        RelationShapeGuards.RequireLegalTargetShape(RelationKind.Targets, operation);
    }

    [Fact]
    [Trait("Requirement", "TAX-50")]
    public void RequireLegalTargetShape_Targets_AcceptsADeploymentUnit()
    {
        var unit = DeploymentUnit.Create(AcmeSolution, "payments-container");

        RelationShapeGuards.RequireLegalTargetShape(RelationKind.Targets, unit);
    }

    [Fact]
    [Trait("Requirement", "TAX-50")]
    public void RequireLegalTargetShape_Targets_AcceptsAnExternalSystem()
    {
        var system = ExternalSystem.Create(AcmeSolution, StructuralLiteral.Create(LiteralRole.ClientName, "stripe", "name"));

        RelationShapeGuards.RequireLegalTargetShape(RelationKind.Targets, system);
    }

    [Fact]
    [Trait("Requirement", "TAX-50")]
    public void RequireLegalTargetShape_Targets_RejectsAnOutboundBoundaryOperation()
    {
        var operation = BoundaryOperation.Create(
            MethodSymbol("Charge").Reference,
            ComponentReference("payments-api"),
            BoundaryDirection.Outbound,
            BoundaryProtocol.Http,
            "external",
            "GET",
            StructuralLiteral.Create(LiteralRole.Route, "/v1/charges", "route"));

        var exception = Assert.Throws<ArgumentException>(
            () => RelationShapeGuards.RequireLegalTargetShape(RelationKind.Targets, operation));

        Assert.Equal("target", exception.ParamName);
        Assert.Contains(nameof(RelationKind.Targets), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-50")]
    public void RequireLegalTargetShape_Targets_RejectsASymbol()
    {
        var symbol = MethodSymbol("Charge");

        var exception = Assert.Throws<ArgumentException>(
            () => RelationShapeGuards.RequireLegalTargetShape(RelationKind.Targets, symbol));

        Assert.Equal("target", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-51")]
    public void RequireLegalTargetShape_OperatesOn_AcceptsADataObjectAndADataField()
    {
        var store = DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders-db", "name"));
        var dataObject = DataObject.Create(
            store.Reference,
            DataObjectForm.Table,
            StructuralLiteral.Create(LiteralRole.SchemaName, "dbo", "schemaName"),
            StructuralLiteral.Create(LiteralRole.TableName, "Orders", "tableName"),
            MappingStateKind.ExplicitConfirmation);
        var dataField = DataField.Create(
            dataObject.Reference, StructuralLiteral.Create(LiteralRole.FieldName, "CustomerId", "fieldName"), MappingStateKind.ExplicitConfirmation);

        RelationShapeGuards.RequireLegalTargetShape(RelationKind.OperatesOn, dataObject);
        RelationShapeGuards.RequireLegalTargetShape(RelationKind.OperatesOn, dataField);
    }

    [Fact]
    [Trait("Requirement", "TAX-51")]
    public void RequireLegalTargetShape_OperatesOn_RejectsADataStore()
    {
        var store = DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders-db", "name"));

        var exception = Assert.Throws<ArgumentException>(
            () => RelationShapeGuards.RequireLegalTargetShape(RelationKind.OperatesOn, store));

        Assert.Equal("target", exception.ParamName);
        Assert.Contains(nameof(RelationKind.OperatesOn), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-53")]
    public void RequireSufficientEvidence_SyntacticSuppliedWhereSemanticRequired_IsRejectedNamingBothMethods()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => RelationShapeGuards.RequireSufficientEvidence(Registry, RelationKind.BelongsTo, EvidenceMethod.Syntactic));

        Assert.Contains(nameof(EvidenceMethod.Semantic), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EvidenceMethod.Syntactic), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-53")]
    public void RequireSufficientEvidence_MatchingMinimum_DoesNotThrow() =>
        RelationShapeGuards.RequireSufficientEvidence(Registry, RelationKind.BelongsTo, EvidenceMethod.Semantic);
}
