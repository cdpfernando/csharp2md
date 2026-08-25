using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class MappingRolesTests
{
    [Fact]
    [Trait("Requirement", "TAX-48")]
    public void MappingRoleTable_HasExactlyTheFourDocumentedRoles()
    {
        var expected = new HashSet<string> { "contract-implementation", "data-object-mapping", "data-field-mapping", "serialization-binding" };
        var actual = MappingRoleTable.All.ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-48")]
    public void RequireRegistered_OutOfVocabularyRole_ThrowsNamingAxisAndValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => MappingRoleTable.RequireRegistered("bogus-role"));

        Assert.Contains("mapping_role", exception.Message, StringComparison.Ordinal);
        Assert.Contains("bogus-role", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-48")]
    public void RequireRegistered_RegisteredRole_DoesNotThrow() =>
        MappingRoleTable.RequireRegistered("contract-implementation");

    [Fact]
    [Trait("Requirement", "TAX-48")]
    public void PayloadRoleTable_VocabularyIsClosedAndNonEmpty()
    {
        var expected = new HashSet<string> { "request", "response", "header", "query-parameter" };
        var actual = PayloadRoleTable.All.ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-48")]
    public void TaxonomyTables_MappingAndPayloadRolesAreReachableOrderedArrays()
    {
        Assert.Equal(
            typeof(ImmutableArray<string>),
            typeof(TaxonomyTables).GetProperty(nameof(TaxonomyTables.MappingRoles))!.PropertyType);
        Assert.Equal(
            typeof(ImmutableArray<string>),
            typeof(TaxonomyTables).GetProperty(nameof(TaxonomyTables.PayloadRoles))!.PropertyType);

        var tables = new TaxonomyTables { MappingRoles = MappingRoleTable.All, PayloadRoles = PayloadRoleTable.All };

        Assert.Equal(MappingRoleTable.All, tables.MappingRoles);
        Assert.Equal(PayloadRoleTable.All, tables.PayloadRoles);
    }
}
