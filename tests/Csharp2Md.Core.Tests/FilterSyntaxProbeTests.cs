namespace Csharp2Md.Core.Tests;

public sealed class FilterSyntaxProbeTests
{
    [Fact]
    public void UntaggedTest_HasNoCategory()
    {
        Assert.True(true);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void IntegrationTaggedTest_HasCategoryIntegration()
    {
        Assert.True(true);
    }
}
