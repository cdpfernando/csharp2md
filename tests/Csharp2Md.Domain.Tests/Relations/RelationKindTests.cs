using Csharp2Md.Domain.Relations;
using Csharp2Md.Domain.Tests.Facets;

namespace Csharp2Md.Domain.Tests.Relations;

public sealed class RelationKindTests
{
    private static readonly string[] ForbiddenInverseNames =
    [
        "ContainedIn", // inverse of Contains
        "Owns", // inverse of BelongsTo
        "Includes", // inverse of IncludedIn
        "ExecutedBy", // inverse of Executes
        "InvokedBy", // inverse of Invokes
        "OperationImplementedBy", // inverse of ImplementsOperation
        "TargetedBy", // inverse of Targets
        "ContractUsedBy", // inverse of UsesContract
        "DataAccessedBy", // inverse of AccessesData
        "OperatedOnBy", // inverse of OperatesOn
        "MappedFrom", // inverse of MapsTo
        "Configures", // inverse of ConfiguredBy
    ];

    [Fact]
    [Trait("Requirement", "TAX-42")]
    public void RelationKind_HasExactlyTheTwelveDocumentedRelations()
    {
        var expected = new HashSet<string>
        {
            "contains", "belongs-to", "included-in", "executes", "invokes", "implements-operation",
            "targets", "uses-contract", "accesses-data", "operates-on", "maps-to", "configured-by",
        };
        var actual = Enum.GetNames<RelationKind>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-43")]
    public void RelationKind_HasNoGenericReferencesOrDependsOnMember()
    {
        var names = Enum.GetNames<RelationKind>();

        Assert.DoesNotContain("References", names);
        Assert.DoesNotContain("DependsOn", names);
    }

    [Fact]
    [Trait("Requirement", "TAX-47")]
    public void RelationKind_HasNoMemberThatIsTheInverseOfAnother()
    {
        var names = Enum.GetNames<RelationKind>().ToHashSet();

        var offendingInverse = ForbiddenInverseNames.FirstOrDefault(names.Contains);

        Assert.True(offendingInverse is null, $"'{offendingInverse}' is the inverse of an existing relation and must not be defined.");
    }
}
