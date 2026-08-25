using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class SolutionIdTests
{
    [Fact]
    [Trait("Requirement", "TAX-73")]
    public void Create_SameLogicalInputsUnderTwoSimulatedAbsoluteRoots_AreByteIdentical()
    {
        var underRootA = BuildUnderRootA();
        var underRootB = BuildUnderRootB();

        Assert.Equal(underRootA.Value, underRootB.Value);

        static SolutionId BuildUnderRootA() =>
            SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln");

        static SolutionId BuildUnderRootB() =>
            SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln");
    }

    [Fact]
    [Trait("Requirement", "TAX-72")]
    public void Create_WorkspaceComponentAppearsInIdentity_AndTwoWorkspacesProduceDistinctIdentities()
    {
        var underFirstWorkspace = SolutionId.Create(WorkspaceIdentity.Create("acme"), "src/Acme.sln");
        var underSecondWorkspace = SolutionId.Create(WorkspaceIdentity.Create("widgets"), "src/Acme.sln");

        Assert.Contains("acme", underFirstWorkspace.Value, StringComparison.Ordinal);
        Assert.NotEqual(underFirstWorkspace.Value, underSecondWorkspace.Value);
    }

    [Theory]
    [Trait("Requirement", "TAX-73")]
    [InlineData("/repo/Acme.sln")]
    [InlineData("C:/repo/Acme.sln")]
    [InlineData("src\\Acme.sln")]
    [InlineData("src/./Acme.sln")]
    [InlineData("src/../Acme.sln")]
    public void Create_AbsoluteOrNonNormalizedPath_IsRejectedNamingParameter(string path)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SolutionId.Create(WorkspaceIdentity.Create("acme"), path));

        Assert.Equal("logicalRelativePath", exception.ParamName);
    }

    [Theory]
    [Trait("Requirement", "TAX-72")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingWorkspaceName_IsRejectedNamingParameterWithNoPartialIdentity(string logicalName)
    {
        var exception = Assert.Throws<ArgumentException>(
            () => SolutionId.Create(WorkspaceIdentity.Create(logicalName), "src/Acme.sln"));

        Assert.Equal("logicalName", exception.ParamName);
    }
}
