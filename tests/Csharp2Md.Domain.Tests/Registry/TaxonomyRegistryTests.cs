using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class TaxonomyRegistryTests
{
    private static readonly TaxonomyRegistry Registry = new(new TaxonomyTables
    {
        FactTypes = FactTypeTable.All,
        Relations = RelationTable.All,
    });

    [Fact]
    [Trait("Requirement", "TAX-44")]
    public void IsRegisteredTriple_RegisteredTriple_ReturnsTrue() =>
        Assert.True(Registry.IsRegisteredTriple(RelationKind.Contains, "Solution", "Project"));

    [Fact]
    [Trait("Requirement", "TAX-44")]
    public void IsRegisteredTriple_UnregisteredTriple_ReturnsFalse() =>
        Assert.False(Registry.IsRegisteredTriple(RelationKind.Contains, "Solution", "Symbol"));

    [Fact]
    [Trait("Requirement", "TAX-45")]
    public void RequireRegisteredTriple_RegisteredTriple_DoesNotThrow() =>
        Registry.RequireRegisteredTriple(RelationKind.Contains, "Solution", "Project");

    [Fact]
    [Trait("Requirement", "TAX-45")]
    public void RequireRegisteredTriple_UnregisteredTriple_ThrowsNamingSourceRelationAndTarget()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Registry.RequireRegisteredTriple(RelationKind.Contains, "Solution", "Symbol"));

        Assert.Contains("Solution", exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(RelationKind.Contains), exception.Message, StringComparison.Ordinal);
        Assert.Contains("Symbol", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-53")]
    public void RequireSufficientEvidence_SyntacticSuppliedWhereSemanticRequired_ThrowsNamingBothMethods()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Registry.RequireSufficientEvidence(RelationKind.BelongsTo, EvidenceMethod.Syntactic));

        Assert.Contains(nameof(EvidenceMethod.Semantic), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EvidenceMethod.Syntactic), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-52")]
    public void MinimumEvidenceMethod_ReturnsTheDeclaredMinimumForTheRelation() =>
        Assert.Equal(EvidenceMethod.Configured, Registry.MinimumEvidenceMethod(RelationKind.IncludedIn));

    [Fact]
    [Trait("Requirement", "TAX-53")]
    public void RequireSufficientEvidence_ExactMatchOfDeclaredMinimum_DoesNotThrow() =>
        Registry.RequireSufficientEvidence(RelationKind.IncludedIn, EvidenceMethod.Configured);

    [Fact]
    [Trait("Requirement", "TAX-53")]
    public void RequireSufficientEvidence_IsNotAnAssumedOrdering_SemanticDoesNotSatisfyAConfiguredMinimum()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Registry.RequireSufficientEvidence(RelationKind.IncludedIn, EvidenceMethod.Semantic));

        Assert.Contains(nameof(EvidenceMethod.Configured), exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EvidenceMethod.Semantic), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "TAX-54")]
    public void EvidenceMethod_HasNoNamePrefixOrPathSimilarityMember()
    {
        var names = Enum.GetNames<EvidenceMethod>();

        Assert.DoesNotContain("NameSimilarity", names);
        Assert.DoesNotContain("PrefixSimilarity", names);
        Assert.DoesNotContain("PathSimilarity", names);
    }
}
