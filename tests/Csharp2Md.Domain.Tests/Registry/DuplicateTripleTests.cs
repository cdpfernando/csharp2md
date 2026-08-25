using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class DuplicateTripleTests
{
    private static TaxonomyTables BuildTablesWithADuplicatedContainsTriple()
    {
        var original = TaxonomyTables.Default;
        var contains = original.Relations.Single(relation => relation.Kind == RelationKind.Contains);
        var duplicated = contains with { Triples = contains.Triples.Add(contains.Triples[0]) };

        return original with { Relations = original.Relations.Replace(contains, duplicated) };
    }

    [Fact]
    [Trait("Requirement", "TAX-90")]
    public void Write_DuplicatedTriple_ThrowsNamingTheRelationAndBothFactTypes()
    {
        var tablesWithDuplicate = BuildTablesWithADuplicatedContainsTriple();
        var duplicatedTriple = TaxonomyTables.Default.Relations.Single(relation => relation.Kind == RelationKind.Contains).Triples[0];

        var exception = Assert.Throws<ArgumentException>(() => TaxonomyRegistryWriter.Write(tablesWithDuplicate));

        Assert.Contains(nameof(RelationKind.Contains), exception.Message, StringComparison.Ordinal);
        Assert.Contains(duplicatedTriple.SourceFactType, exception.Message, StringComparison.Ordinal);
        Assert.Contains(duplicatedTriple.TargetFactType, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-90")]
    public void TaxonomyRegistry_ConstructedWithTheSameDuplicatedTriple_ThrowsTheSameGuardTheEmitterInherits()
    {
        var tablesWithDuplicate = BuildTablesWithADuplicatedContainsTriple();
        var duplicatedTriple = TaxonomyTables.Default.Relations.Single(relation => relation.Kind == RelationKind.Contains).Triples[0];

        var exception = Assert.Throws<ArgumentException>(() => new TaxonomyRegistry(tablesWithDuplicate));

        Assert.Contains(nameof(RelationKind.Contains), exception.Message, StringComparison.Ordinal);
        Assert.Contains(duplicatedTriple.SourceFactType, exception.Message, StringComparison.Ordinal);
        Assert.Contains(duplicatedTriple.TargetFactType, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-90")]
    public void BuildingTheDuplicatedCopy_LeavesTheProductionTableSetUnmodifiedAndStillValid()
    {
        var beforeTripleCount = TaxonomyTables.Default.Relations
            .Single(relation => relation.Kind == RelationKind.Contains).Triples.Length;

        _ = BuildTablesWithADuplicatedContainsTriple();

        var afterTripleCount = TaxonomyTables.Default.Relations
            .Single(relation => relation.Kind == RelationKind.Contains).Triples.Length;
        Assert.Equal(beforeTripleCount, afterTripleCount);

        var registry = new TaxonomyRegistry(TaxonomyTables.Default);
        Assert.True(registry.IsRegisteredTriple(RelationKind.Contains, "Solution", "Project"));
    }
}
