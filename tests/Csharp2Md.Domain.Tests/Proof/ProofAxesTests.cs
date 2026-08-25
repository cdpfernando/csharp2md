using System.Reflection;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Tests.Facets;

namespace Csharp2Md.Domain.Tests.Proof;

public sealed class ProofAxesTests
{
    [Fact]
    [Trait("Requirement", "TAX-55")]
    public void EvidenceMethod_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "semantic", "syntactic", "configured" };
        var actual = Enum.GetNames<EvidenceMethod>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-56")]
    public void Resolution_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "confirmed", "candidate", "unresolved" };
        var actual = Enum.GetNames<Resolution>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-57")]
    public void Frontier_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "closed", "open" };
        var actual = Enum.GetNames<Frontier>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-58")]
    public void Proof_HasNoEnumCombiningEvidenceMethodResolutionAndFrontier()
    {
        var evidenceNames = Enum.GetNames<EvidenceMethod>().ToHashSet();
        var resolutionNames = Enum.GetNames<Resolution>().ToHashSet();
        var frontierNames = Enum.GetNames<Frontier>().ToHashSet();

        var enumTypes = typeof(EvidenceMethod).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace == typeof(EvidenceMethod).Namespace);

        foreach (var enumType in enumTypes)
        {
            var memberNames = Enum.GetNames(enumType).ToHashSet();
            var axesRepresented = new[] { evidenceNames, resolutionNames, frontierNames }
                .Count(axis => axis.Overlaps(memberNames));

            Assert.True(
                axesRepresented <= 1,
                $"Enum '{enumType.Name}' combines members from more than one proof-state axis.");
        }
    }

    [Fact]
    [Trait("Requirement", "TAX-58")]
    public void Proof_HasNoStrongerOrRankPrecedenceHelper()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        var offendingMethod = typeof(EvidenceMethod).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(EvidenceMethod).Namespace)
            .SelectMany(type => type.GetMethods(flags))
            .FirstOrDefault(method => method.Name is "Stronger" or "Rank");

        Assert.True(offendingMethod is null, $"No 'Stronger' or 'Rank' precedence helper may be ported into {typeof(EvidenceMethod).Namespace}.");
    }
}
