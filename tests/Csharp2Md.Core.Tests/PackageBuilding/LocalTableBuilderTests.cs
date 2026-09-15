using Csharp2Md.Core.PackageBuilding.Identity;
namespace Csharp2Md.Core.Tests.PackageBuilding;
public sealed class LocalTableBuilderTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(1, "1")]
    [InlineData(9, "9")]
    [InlineData(10, "a")]
    [InlineData(35, "z")]
    [InlineData(36, "10")]
    [InlineData(37, "11")]
    [InlineData(71, "1z")]
    [InlineData(72, "20")]
    [Trait("Requirement", "STO-03")]
    public void Build_AssignsLowercaseBase36HandlesInCanonicalOrder(int ordinal, string handle)
    {
        var keys = Enumerable.Range(0, ordinal + 1).Select(value => $"key:{value:D8}").Reverse();
        Assert.Equal(handle, LocalTableBuilder.Build("solution:a", keys).Resolve($"key:{ordinal:D8}").Value);
    }
    [Fact][Trait("Requirement", "STO-03")] public void Build_DeduplicatesCanonicalKeys() => Assert.Single(LocalTableBuilder.Build("s", ["a", "a"]).Handles);
    [Fact][Trait("Requirement", "STO-04")] public void Build_DoesNotShareHandlesAcrossSolutions() { var a=LocalTableBuilder.Build("a",["a","b"]); var b=LocalTableBuilder.Build("b",["b"]); Assert.Equal("1",a.Resolve("b").Value); Assert.Equal("0",b.Resolve("b").Value); }
    [Fact][Trait("Requirement", "STO-05")] public void Resolve_MissingKeyRejectsDirectLookup() => Assert.Throws<KeyNotFoundException>(() => LocalTableBuilder.Build("s", ["a"]).Resolve("b"));
    [Fact][Trait("Requirement", "VAR-06")] public void Build_UsesCanonicalKeysNotInputOrder() { var a=LocalTableBuilder.Build("s",["z","a"]); var b=LocalTableBuilder.Build("s",["a","z"]); Assert.Equal(a.Handles,b.Handles); }
    [Fact][Trait("Requirement", "STO-03")] public void LocalHandle_IsLimitedToSixCharacters() => Assert.True(LocalTableBuilder.Build("s", ["a"]).Resolve("a").Value.Length <= 6);
}
