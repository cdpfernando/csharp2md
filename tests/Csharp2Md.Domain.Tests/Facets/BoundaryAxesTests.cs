using Csharp2Md.Domain.Facets;

namespace Csharp2Md.Domain.Tests.Facets;

public sealed class BoundaryAxesTests
{
    [Fact]
    [Trait("Requirement", "TAX-18")]
    public void BoundaryProtocol_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "http", "grpc", "messaging", "cli", "scheduler", "function" };
        var actual = Enum.GetNames<BoundaryProtocol>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-19")]
    public void BoundaryDirection_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "inbound", "outbound" };
        var actual = Enum.GetNames<BoundaryDirection>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-20")]
    public void BoundaryRole_HasExactlyTheDocumentedValues()
    {
        var expected = new HashSet<string> { "command", "query", "event", "stream", "lifecycle" };
        var actual = Enum.GetNames<BoundaryRole>().Select(FacetWireNames.ToKebabCase).ToHashSet();

        Assert.Empty(expected.Except(actual).Concat(actual.Except(expected)));
    }

    [Fact]
    [Trait("Requirement", "TAX-21")]
    public void Facets_HasNoEnumCombiningProtocolDirectionAndRole()
    {
        var protocolNames = Enum.GetNames<BoundaryProtocol>().ToHashSet();
        var directionNames = Enum.GetNames<BoundaryDirection>().ToHashSet();
        var roleNames = Enum.GetNames<BoundaryRole>().ToHashSet();

        var enumTypes = typeof(BoundaryProtocol).Assembly.GetTypes()
            .Where(type => type.IsEnum && type.Namespace == typeof(BoundaryProtocol).Namespace);

        foreach (var enumType in enumTypes)
        {
            var memberNames = Enum.GetNames(enumType).ToHashSet();
            var axesRepresented = new[] { protocolNames, directionNames, roleNames }
                .Count(axis => axis.Overlaps(memberNames));

            Assert.True(
                axesRepresented <= 1,
                $"Enum '{enumType.Name}' combines members from more than one boundary-operation axis.");
        }
    }
}
