using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Domain.Tests.Identity;

public sealed class WorkspaceIdentityTests
{
    [Fact]
    [Trait("Requirement", "TAX-72")]
    public void Create_TwoDistinctLogicalNames_ProduceDistinctIdentities()
    {
        var first = WorkspaceIdentity.Create("acme");
        var second = WorkspaceIdentity.Create("widgets");

        Assert.NotEqual(first.Value, second.Value);
    }

    [Theory]
    [Trait("Requirement", "TAX-72")]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_MissingLogicalName_IsRejectedNamingParameterWithNoPartialIdentity(string logicalName)
    {
        var exception = Assert.Throws<ArgumentException>(() => WorkspaceIdentity.Create(logicalName));

        Assert.Equal("logicalName", exception.ParamName);
    }
}
