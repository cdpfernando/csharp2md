using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Facts;

public sealed class FactFamilyClosureTests
{
    private static readonly string[] FacetOnlyOrBusinessRuleNames =
        ["Controller", "Handler", "Repository", "Client", "Service", "Callable", "BusinessRule", "Rule"];

    private static IFact[] AllInstances()
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solution = SolutionId.Create(workspace, "src/Acme.sln");
        var project = ProjectId.Create(solution, "src/Acme.Payments/Acme.Payments.csproj");
        var signature = CanonicalSymbolSignature.Create("method", "global::Acme.Payment", "Charge", 0, "global::System.Void");

        var symbolFact = Symbol.Create(signature, project, SymbolFacetSet.Create([SymbolFacet.Callable]));
        var component = Component.Create(solution, "Payments.Api", [symbolFact.Reference]);
        var externalSystem = ExternalSystem.Create(solution, StructuralLiteral.Create(LiteralRole.ClientName, "stripe", "name"));
        var entryPoint = EntryPoint.Create(symbolFact.Reference, component.Reference);
        var boundaryOperation = BoundaryOperation.Create(
            symbolFact.Reference,
            component.Reference,
            BoundaryDirection.Inbound,
            protocolOperationKey: StructuralLiteral.Create(LiteralRole.ProtocolName, "POST /charge", "protocolOperationKey"));

        var contract = Contract.Create(StructuralLiteral.Create(LiteralRole.SchemaName, "orders.v1.OrderPlaced", "proof"));
        var contractBinding = ContractBinding.Create(
            boundaryOperation.Reference, PayloadRoleTable.All[0], symbolFact.Reference, contract.Reference);
        var contractRevision = ContractRevision.Create(contract.Reference, "shape-v1");

        var dataStore = DataStore.Create(DataStoreTechnology.Relational, StructuralLiteral.Create(LiteralRole.ClientName, "orders-db", "name"));
        var dataObject = DataObject.Create(
            dataStore.Reference,
            DataObjectForm.Table,
            StructuralLiteral.Create(LiteralRole.SchemaName, "dbo", "schemaName"),
            StructuralLiteral.Create(LiteralRole.TableName, "Orders", "tableName"),
            MappingStateKind.Unresolved);
        var dataField = DataField.Create(
            dataObject.Reference, StructuralLiteral.Create(LiteralRole.FieldName, "CustomerId", "fieldName"), MappingStateKind.Unresolved);
        var dataOperation = DataOperation.Create(dataObject.Reference, DataOperationKind.Read, MappingStateKind.Unresolved);

        var projectFact = Project.Create(project);
        var configurationBinding = ConfigurationBinding.Create(
            projectFact.Reference, StructuralLiteral.Create(LiteralRole.ConfigurationKey, "ConnectionStrings:Default", "configurationKey"));

        return
        [
            Solution.Create(solution),
            projectFact,
            Document.Create(project, "src/Acme.Payments/Invoice.cs"),
            symbolFact,
            component,
            DeploymentUnit.Create(solution, "Payments.Container"),
            entryPoint,
            boundaryOperation,
            externalSystem,
            contract,
            contractBinding,
            contractRevision,
            dataStore,
            dataObject,
            dataField,
            dataOperation,
            configurationBinding,
        ];
    }

    [Fact]
    [Trait("Requirement", "TAX-07")]
    public void ExactlyFiveFamiliesAreDefined_AssertedByReflectionOverTheEnum() =>
        Assert.Equal(5, Enum.GetValues<FactFamily>().Length);

    [Fact]
    [Trait("Requirement", "TAX-07")]
    public void ExactlySeventeenClrTypesImplementIFact_AssertedByReflectionOverTheAssembly()
    {
        var clrFactTypes = typeof(IFact).Assembly.GetTypes()
            .Where(type => typeof(IFact).IsAssignableFrom(type) && !type.IsInterface)
            .ToArray();

        Assert.Equal(17, clrFactTypes.Length);
    }

    [Fact]
    [Trait("Requirement", "TAX-07")]
    public void SymmetricDifference_BetweenRegistryDescriptorsAndClrRecords_IsEmptyInBothDirections()
    {
        var registryNames = FactTypeTable.All.Select(descriptor => descriptor.Name).ToHashSet(StringComparer.Ordinal);
        var clrNames = typeof(IFact).Assembly.GetTypes()
            .Where(type => typeof(IFact).IsAssignableFrom(type) && !type.IsInterface)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missingFromClr = registryNames.Except(clrNames).ToArray();
        var extraInClr = clrNames.Except(registryNames).ToArray();

        Assert.True(
            missingFromClr.Length == 0 && extraInClr.Length == 0,
            $"Fact-family drift: missing CLR record(s) for registry type(s) [{string.Join(", ", missingFromClr)}], " +
            $"CLR record(s) with no registry descriptor [{string.Join(", ", extraInClr)}].");
    }

    [Fact]
    [Trait("Requirement", "TAX-13")]
    public void NoClrFactTypeName_MatchesAFacetOnlyRoleOrABusinessRuleConcept()
    {
        var clrNames = typeof(IFact).Assembly.GetTypes()
            .Where(type => typeof(IFact).IsAssignableFrom(type) && !type.IsInterface)
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var offending = FacetOnlyOrBusinessRuleNames.Where(clrNames.Contains).ToArray();

        Assert.True(offending.Length == 0, $"Fact type name(s) collide with a facet-only role or business-rule concept: [{string.Join(", ", offending)}].");
    }

    [Fact]
    [Trait("Requirement", "TAX-07")]
    public void EveryIFactImplementation_IsReachableFromExactlyOneFamily_MatchingItsRegistryDescriptor()
    {
        var registryByName = FactTypeTable.All.ToDictionary(descriptor => descriptor.Name, StringComparer.Ordinal);
        var instances = AllInstances();

        Assert.Equal(17, instances.Length);
        Assert.All(instances, fact =>
        {
            var typeName = fact.GetType().Name;
            Assert.True(registryByName.TryGetValue(typeName, out var descriptor), $"'{typeName}' has no registry descriptor.");
            Assert.Equal(descriptor!.Family, fact.Family);
        });

        var familyByType = instances.ToDictionary(fact => fact.GetType().Name, fact => fact.Family, StringComparer.Ordinal);
        Assert.Equal(instances.Length, familyByType.Count);
    }
}
