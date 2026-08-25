using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class ObservationKindDescriptorTests
{
    [Theory]
    [Trait("Requirement", "TAX-33")]
    [InlineData(ObservationKind.Invocation)]
    [InlineData(ObservationKind.ObjectCreation)]
    [InlineData(ObservationKind.TypeUsage)]
    [InlineData(ObservationKind.BaseType)]
    [InlineData(ObservationKind.AttributeUsage)]
    public void ObservationKindTable_MarksKindAsAlwaysWhenBindable(ObservationKind kind)
    {
        var descriptor = ObservationKindTable.All.Single(entry => entry.Kind == kind);

        Assert.Equal(EmissionTier.AlwaysWhenBindable, descriptor.Tier);
    }

    [Theory]
    [Trait("Requirement", "TAX-34")]
    [InlineData(ObservationKind.Assignment)]
    [InlineData(ObservationKind.Configuration)]
    [InlineData(ObservationKind.RouteDeclaration)]
    [InlineData(ObservationKind.MessageOperation)]
    [InlineData(ObservationKind.DataAccess)]
    public void ObservationKindTable_MarksKindAsRegisteredContextOnly(ObservationKind kind)
    {
        var descriptor = ObservationKindTable.All.Single(entry => entry.Kind == kind);

        Assert.Equal(EmissionTier.RegisteredContextOnly, descriptor.Tier);
    }

    [Fact]
    [Trait("Requirement", "TAX-33")]
    public void ObservationKindTable_CoversEveryEnumMemberExactlyOnce()
    {
        var allMembers = Enum.GetValues<ObservationKind>();
        var declaredKinds = ObservationKindTable.All.Select(entry => entry.Kind).ToArray();

        Assert.Equal(allMembers.Length, declaredKinds.Length);
        Assert.Equal(allMembers.Length, declaredKinds.Distinct().Count());
        Assert.Empty(allMembers.Except(declaredKinds));
    }

    [Fact]
    [Trait("Requirement", "TAX-33")]
    public void TaxonomyTables_ObservationKindsIsAReachableOrderedArray()
    {
        Assert.Equal(
            typeof(ImmutableArray<ObservationKindDescriptor>),
            typeof(TaxonomyTables).GetProperty(nameof(TaxonomyTables.ObservationKinds))!.PropertyType);

        var tables = new TaxonomyTables { ObservationKinds = ObservationKindTable.All };

        Assert.Equal(ObservationKindTable.All, tables.ObservationKinds);
    }
}
