using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Tests.Storage;

public sealed class SolutionCoordinateTests
{
    [Fact]
    [Trait("Requirement", "MSC-02")]
    public void For_SameFileNameUnderTwoAbsoluteParents_ReturnsEqualIdentity()
    {
        var left = SolutionCoordinate.For(Path.Combine(Path.GetTempPath(), "clone-a", "Acme.Orders.slnx"));
        var right = SolutionCoordinate.For(Path.Combine(Path.GetTempPath(), "clone-b", "Acme.Orders.slnx"));

        Assert.Equal(left.Identity, right.Identity);
        Assert.Equal("Acme.Orders.slnx", left.SolutionFileName);
        Assert.Equal("Acme.Orders.slnx", right.SolutionFileName);
        Assert.DoesNotContain("clone-a", left.Identity.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("clone-b", right.Identity.Value, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Requirement", "MSC-02")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void For_NullEmptyOrWhitespacePath_ThrowsArgumentException(string? solutionPath)
    {
        Assert.ThrowsAny<ArgumentException>(() => SolutionCoordinate.For(solutionPath!));
    }

    [Fact]
    [Trait("Requirement", "MSC-02")]
    public void For_PathContainingSpaces_IdentityIsTheFileNameNotTheParent()
    {
        var parent = Path.Combine(Path.GetTempPath(), "My Solutions");
        var path = Path.Combine(parent, "Acme Orders.slnx");

        var coordinate = SolutionCoordinate.For(path);

        Assert.Equal("Acme Orders.slnx", coordinate.SolutionFileName);
        Assert.Equal(
            SolutionId.Create(WorkspaceIdentity.Create("default"), "Acme Orders.slnx"),
            coordinate.Identity);
        Assert.DoesNotContain("My Solutions", coordinate.Identity.Value, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-02")]
    public void For_NonAsciiSolutionName_IdentityIsTheFileNameNotTheParent()
    {
        var parent = Path.Combine(Path.GetTempPath(), "repos");
        var path = Path.Combine(parent, "Café.Orders.slnx");

        var coordinate = SolutionCoordinate.For(path);

        Assert.Equal("Café.Orders.slnx", coordinate.SolutionFileName);
        Assert.Equal(
            SolutionId.Create(WorkspaceIdentity.Create("default"), "Café.Orders.slnx"),
            coordinate.Identity);
        Assert.DoesNotContain("repos", coordinate.Identity.Value, StringComparison.Ordinal);
    }
}
