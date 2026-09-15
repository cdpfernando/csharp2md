using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests.Mapping;

public sealed class FactRoundTripTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");

    private static ProjectId AcmeProject => ProjectId.Create(AcmeSolution, "src/Acme.Payments/Acme.Payments.csproj");

    private static CanonicalSymbolSignature RunSignature => CanonicalSymbolSignature.Create(
        "method", "global::Acme.Payment", "Run", 0, "global::System.Void");

    public static TheoryData<string, FactualSnapshot> FactFixtures() => new()
    {
        { "Solution", Snapshot(Solution.Create(AcmeSolution)) },
        { "Project", Snapshot(Project.Create(AcmeProject)) },
        { "Document", Snapshot(Document.Create(AcmeProject, "src/Acme.Payments/Invoice.cs")) },
        { "Symbol", Snapshot(Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable]))) },
        { "Component", Snapshot(Component.Create(AcmeSolution, "Payments.Api", [Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([])).Reference])) },
        { "DeploymentUnit", Snapshot(DeploymentUnit.Create(AcmeSolution, "Payments.Container")) },
        { "EntryPoint", Snapshot(EntryPoint.Create(SymbolRef(), ComponentRef())) },
        { "BoundaryOperation", Snapshot(BoundaryOperation.Create(
            SymbolRef(),
            ComponentRef(),
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey"))) },
        { "ExternalSystem", Snapshot(ExternalSystem.Create(
            AcmeSolution, StructuralLiteral.Create(LiteralRole.ClientName, "stripe", "name"))) },
        { "Contract", Snapshot(Contract.Create(StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof"))) },
        { "ContractBinding", Snapshot(ContractBinding.Create(
            BoundaryRef(), "request", SymbolRef(), Contract.Create(StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof")).Reference)) },
        { "ContractRevision", Snapshot(ContractRevision.Create(
            Contract.Create(StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof")).Reference,
            "fp-1")) },
        { "DataStore", Snapshot(DataStore.Create(
            DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders", "name"))) },
        { "DataObject", Snapshot(DataObject.Create(
            DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders", "name")).Reference,
            DataObjectForm.Table,
            StructuralLiteral.Create(LiteralRole.SchemaName, "dbo", "schemaName"),
            StructuralLiteral.Create(LiteralRole.TableName, "Orders", "tableName"),
            MappingStateKind.ExplicitConfirmation)) },
        { "DataField", Snapshot(DataField.Create(
            DataObject.Create(
                DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders", "name")).Reference,
                DataObjectForm.Table,
                StructuralLiteral.Create(LiteralRole.SchemaName, "dbo", "schemaName"),
                StructuralLiteral.Create(LiteralRole.TableName, "Orders", "tableName"),
                MappingStateKind.ExplicitConfirmation).Reference,
            StructuralLiteral.Create(LiteralRole.FieldName, "Id", "fieldName"),
            MappingStateKind.ExplicitConfirmation)) },
        { "DataOperation", Snapshot(DataOperation.Create(
            DataObject.Create(
                DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders", "name")).Reference,
                DataObjectForm.Table,
                StructuralLiteral.Create(LiteralRole.SchemaName, "dbo", "schemaName"),
                StructuralLiteral.Create(LiteralRole.TableName, "Orders", "tableName"),
                MappingStateKind.ExplicitConfirmation).Reference,
            DataOperationKind.Read,
            MappingStateKind.ExplicitConfirmation)) },
        { "ConfigurationBinding", Snapshot(ConfigurationBinding.Create(
            Project.Create(AcmeProject).Reference,
            StructuralLiteral.Create(LiteralRole.ConfigurationKey, "ConnectionStrings:Orders", "configurationKey"))) },
    };

    [Theory]
    [Trait("Requirement", "STOR-01")]
    [Trait("Requirement", "STOR-10")]
    [Trait("Requirement", "STOR-13")]
    [Trait("Requirement", "APR-21")]
    [MemberData(nameof(FactFixtures))]
    public void RoundTrip_FactType_EqualsOriginalUnderDomainEquality(string factType, FactualSnapshot snapshot)
    {
        Assert.Equal(factType, snapshot.Facts[0].Reference.FactType);

        var restored = DomainMapper.FromWire(DomainMapper.ToWire(snapshot, Context));

        Assert.Equal(snapshot.Facts.Length, restored.Facts.Length);
        Assert.Equal(snapshot.Facts[0].Reference.FactType, restored.Facts[0].Reference.FactType);
        Assert.Equal(snapshot.Facts[0], restored.Facts[0]);
        Assert.True(restored.Observations.IsEmpty);
        Assert.True(restored.ConfirmedRelations.IsEmpty);
    }

    private static FactualSnapshot Snapshot(IFact fact) =>
        new([fact], [], [], [], [], []);

    private static FactReference SymbolRef() =>
        Symbol.Create(RunSignature, AcmeProject, SymbolFacetSet.Create([SymbolFacet.Callable])).Reference;

    private static FactReference ComponentRef() =>
        Component.Create(AcmeSolution, "Payments.Api", []).Reference;

    private static FactReference BoundaryRef() =>
        BoundaryOperation.Create(
            SymbolRef(),
            ComponentRef(),
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey")).Reference;
}
