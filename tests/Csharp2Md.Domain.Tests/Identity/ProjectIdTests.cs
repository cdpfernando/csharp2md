using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class ProjectIdTests
{
    private static SolutionId Solution => SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln");

    [Fact]
    [Trait("Requirement", "TAX-74")]
    public void Create_WithoutLogicalKey_MovingToADifferentPath_YieldsADifferentIdentity()
    {
        var beforeMove = ProjectId.Create(Solution, "src/Acme.Payments/Acme.Payments.csproj");
        var afterMove = ProjectId.Create(Solution, "src/Moved/Acme.Payments.csproj");

        Assert.NotEqual(beforeMove.Value, afterMove.Value);
    }

    [Fact]
    [Trait("Requirement", "TAX-75")]
    public void Create_WithLogicalKey_MovingToADifferentPath_YieldsTheIdenticalIdentity()
    {
        var key = LogicalKey.Create("acme-payments");

        var beforeMove = ProjectId.Create(Solution, "src/Acme.Payments/Acme.Payments.csproj", key);
        var afterMove = ProjectId.Create(Solution, "src/Moved/Acme.Payments.csproj", key);

        Assert.Equal(beforeMove.Value, afterMove.Value);
    }

    [Theory]
    [Trait("Requirement", "TAX-75")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("key  with  spaces")]
    [InlineData(" leading")]
    public void LogicalKey_NonCanonicalText_IsRejectedNamingParameter(string key)
    {
        var exception = Assert.Throws<ArgumentException>(() => LogicalKey.Create(key));

        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    [Trait("Requirement", "TAX-74")]
    public void Create_SameRelativePathUnderTwoSolutions_ProducesDistinctIdentities()
    {
        var firstSolution = SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln");
        var secondSolution = SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Other.sln");

        var underFirstSolution = ProjectId.Create(firstSolution, "src/Acme.Payments/Acme.Payments.csproj");
        var underSecondSolution = ProjectId.Create(secondSolution, "src/Acme.Payments/Acme.Payments.csproj");

        Assert.NotEqual(underFirstSolution.Value, underSecondSolution.Value);
    }
}
