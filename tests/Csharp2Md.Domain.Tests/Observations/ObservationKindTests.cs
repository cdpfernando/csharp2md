using Csharp2Md.Domain.Observations;

namespace Csharp2Md.Domain.Tests.Observations;

public sealed class ObservationKindTests
{
    private static readonly string[] ForbiddenRoslynConceptSubstrings = ["Syntax", "Symbol", "Node", "Expression"];

    [Fact]
    [Trait("Requirement", "TAX-32")]
    public void ObservationKind_HasExactlyTheDocumentedKinds()
    {
        var expected = new HashSet<string>
        {
            "Invocation", "ObjectCreation", "TypeUsage", "BaseType", "AttributeUsage",
            "Assignment", "Configuration", "RouteDeclaration", "MessageOperation", "DataAccess",
        };
        var actual = Enum.GetNames<ObservationKind>().ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-32")]
    public void EmissionTier_HasExactlyTwoMembers()
    {
        var expected = new HashSet<string> { "AlwaysWhenBindable", "RegisteredContextOnly" };
        var actual = Enum.GetNames<EmissionTier>().ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-32")]
    public void ObservationKind_NoMemberNamesARoslynSyntaxOrSymbolConcept()
    {
        var offendingName = Enum.GetNames<ObservationKind>()
            .FirstOrDefault(name => ForbiddenRoslynConceptSubstrings.Any(name.Contains));

        Assert.True(offendingName is null, $"'{offendingName}' names a Roslyn syntax or symbol concept.");
    }
}
