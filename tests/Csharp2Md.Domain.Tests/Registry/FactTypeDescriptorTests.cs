using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class FactTypeDescriptorTests
{
    private static readonly string[] FacetOnlyRoleNames = ["Controller", "Handler", "Repository", "Client", "Service", "Callable"];

    [Fact]
    [Trait("Requirement", "TAX-07")]
    public void FactFamily_HasExactlyTheFiveDocumentedFamilies()
    {
        var expected = new HashSet<string> { "Structural", "Architecture", "Contract", "Persistence", "Configuration" };
        var actual = Enum.GetNames<FactFamily>().ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void FactTypeTable_StructuralFamily_HasExactlyTheFourDocumentedTypes()
    {
        var expected = new HashSet<string> { "Solution", "Project", "Document", "Symbol" };

        AssertFamilyTypeSet(FactFamily.Structural, expected);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void FactTypeTable_ArchitectureFamily_HasExactlyTheFiveDocumentedTypes()
    {
        var expected = new HashSet<string> { "Component", "DeploymentUnit", "EntryPoint", "BoundaryOperation", "ExternalSystem" };

        AssertFamilyTypeSet(FactFamily.Architecture, expected);
    }

    [Fact]
    [Trait("Requirement", "TAX-10")]
    public void FactTypeTable_ContractFamily_HasExactlyTheThreeDocumentedTypes()
    {
        var expected = new HashSet<string> { "Contract", "ContractBinding", "ContractRevision" };

        AssertFamilyTypeSet(FactFamily.Contract, expected);
    }

    [Fact]
    [Trait("Requirement", "TAX-11")]
    public void FactTypeTable_PersistenceFamily_HasExactlyTheFourDocumentedTypes()
    {
        var expected = new HashSet<string> { "DataStore", "DataObject", "DataField", "DataOperation" };

        AssertFamilyTypeSet(FactFamily.Persistence, expected);
    }

    [Fact]
    [Trait("Requirement", "TAX-12")]
    public void FactTypeTable_ConfigurationFamily_HasExactlyTheOneDocumentedType()
    {
        var expected = new HashSet<string> { "ConfigurationBinding" };

        AssertFamilyTypeSet(FactFamily.Configuration, expected);
    }

    [Fact]
    [Trait("Requirement", "TAX-09")]
    public void FactTypeTable_HasNoFactTypeNamedForAFacetOnlyRole()
    {
        var names = FactTypeTable.All.Select(type => type.Name).ToHashSet();

        var offending = FacetOnlyRoleNames.FirstOrDefault(names.Contains);

        Assert.True(offending is null, $"'{offending}' is a facet-only role and must not be declared as a fact type.");
    }

    [Fact]
    [Trait("Requirement", "TAX-15")]
    public void FactTypeTable_HasNoFactTypeWhoseNameIsABusinessRuleOrEquivalent()
    {
        var names = FactTypeTable.All.Select(type => type.Name);

        Assert.DoesNotContain(names, name => name.Contains("BusinessRule", StringComparison.Ordinal) || name.Contains("Rule", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "TAX-08")]
    public void FactTypeTable_DeclaredOrderIsStableAndTypedAsImmutableArray()
    {
        var expectedOrder = new[]
        {
            "Solution", "Project", "Document", "Symbol",
            "Component", "DeploymentUnit", "EntryPoint", "BoundaryOperation", "ExternalSystem",
            "Contract", "ContractBinding", "ContractRevision",
            "DataStore", "DataObject", "DataField", "DataOperation",
            "ConfigurationBinding",
        };

        Assert.Equal(expectedOrder, FactTypeTable.All.Select(type => type.Name).ToArray());
        Assert.Equal(typeof(ImmutableArray<FactTypeDescriptor>), typeof(TaxonomyTables).GetProperty(nameof(TaxonomyTables.FactTypes))!.PropertyType);
    }

    private static void AssertFamilyTypeSet(FactFamily family, HashSet<string> expected)
    {
        var actual = FactTypeTable.All.Where(type => type.Family == family).Select(type => type.Name).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }
}
