using Csharp2Md.Domain.Facets;

namespace Csharp2Md.Domain.Tests.Facets;

public sealed class PersistenceAxesTests
{
    [Fact]
    [Trait("Requirement", "TAX-22")]
    public void DataStoreTechnology_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "relational", "document", "key-value", "cache", "unknown" };
        var actual = Enum.GetNames<DataStoreTechnology>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-23")]
    public void DataObjectForm_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "table", "view", "collection", "key-space", "cache-region", "unknown" };
        var actual = Enum.GetNames<DataObjectForm>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-24")]
    public void DataOperationKind_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "read", "insert", "update", "delete", "execute", "unknown" };
        var actual = Enum.GetNames<DataOperationKind>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-31")]
    public void MappingStateKind_HasExactlyExplicitConfirmationConventionalCandidateAndUnresolved()
    {
        var expected = new HashSet<string> { "explicit-confirmation", "conventional-candidate", "unresolved" };
        var actual = Enum.GetNames<MappingStateKind>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }
}
