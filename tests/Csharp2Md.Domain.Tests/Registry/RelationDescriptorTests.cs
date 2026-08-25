using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class RelationDescriptorTests
{
    private static readonly HashSet<string> StructuralFactTypeNames =
        FactTypeTable.All.Where(type => type.Family == FactFamily.Structural).Select(type => type.Name).ToHashSet();

    [Fact]
    [Trait("Requirement", "TAX-44")]
    public void RelationTable_EveryRelationDeclaresAtLeastOneTriple()
    {
        foreach (var relation in RelationTable.All)
        {
            Assert.True(relation.Triples.Length > 0, $"'{relation.Kind}' declares no triples.");
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-44")]
    public void RelationTable_ContainsIsRestrictedToStructuralOwnerAndStructuralChild()
    {
        var contains = RelationTable.All.Single(relation => relation.Kind == RelationKind.Contains);

        foreach (var triple in contains.Triples)
        {
            Assert.Contains(triple.SourceFactType, StructuralFactTypeNames);
            Assert.Contains(triple.TargetFactType, StructuralFactTypeNames);
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-52")]
    public void RelationTable_EveryRelationDeclaresADefinedMinimumEvidenceMethod()
    {
        foreach (var relation in RelationTable.All)
        {
            Assert.True(Enum.IsDefined(relation.MinimumEvidenceMethod), $"'{relation.Kind}' has an undefined minimum evidence method.");
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-90")]
    public void RelationTripleIndex_Build_DuplicateTripleDeclaration_ThrowsNamingRelationAndBothFactTypes()
    {
        ImmutableArray<RelationDescriptor> duplicated =
        [
            new(RelationKind.Contains, "contains", [new("Solution", "Project")], EvidenceMethod.Syntactic),
            new(RelationKind.Contains, "contains", [new("Solution", "Project")], EvidenceMethod.Syntactic),
        ];

        var exception = Assert.Throws<ArgumentException>(() => RelationTripleIndex.Build(duplicated));

        Assert.Contains(nameof(RelationKind.Contains), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Solution", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Project", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-44")]
    public void RelationTable_EveryTripleEndpointIsARegisteredFactTypeName()
    {
        var factTypeNames = FactTypeTable.All.Select(type => type.Name).ToHashSet();
        var endpoints = RelationTable.All
            .SelectMany(relation => relation.Triples)
            .SelectMany(triple => new[] { triple.SourceFactType, triple.TargetFactType })
            .ToHashSet();

        Assert.Subset(factTypeNames, endpoints);
    }

    [Fact]
    [Trait("Requirement", "TAX-90")]
    public void RelationTable_TripleLookup_IsDerivedFromTheOrderedArray()
    {
        var expectedKeys = RelationTable.All
            .SelectMany(relation => relation.Triples.Select(triple => new RelationTripleKey(relation.Kind, triple.SourceFactType, triple.TargetFactType)))
            .ToHashSet();

        Assert.Equal(expectedKeys.Count, RelationTable.TripleLookup.Count);
        foreach (var key in expectedKeys)
        {
            Assert.True(RelationTable.TripleLookup.Contains(key), $"'{key}' is missing from the derived frozen lookup.");
        }

        Assert.Equal(
            typeof(ImmutableArray<RelationDescriptor>),
            typeof(TaxonomyTables).GetProperty(nameof(TaxonomyTables.Relations))!.PropertyType);
    }
}
