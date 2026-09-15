using Csharp2Md.Core.PackageBuilding.Navigation;
namespace Csharp2Md.Core.Tests.PackageBuilding;
public sealed class NavigationIndexBuilderTests
{
 [Theory][InlineData("identity")][InlineData("roots")][InlineData("outgoing")][InlineData("incoming")][InlineData("contracts")][InlineData("persistence")][InlineData("evidence")][Trait("Requirement","NAV-01")] public void Build_ResolvesSupportedStartKeyDirectly(string key){var index=NavigationIndexBuilder.Build("solution:a",[(key,"indexes/"+key+".json",2)]);var locator=index.Resolve(key);Assert.Equal("indexes/"+key+".json",locator.ArtifactPath);Assert.Equal(2,locator.Ordinal);}
 [Fact][Trait("Requirement","NAV-04")] public void Resolve_MissingKeyRejectsWithoutShardChoice()=>Assert.Throws<KeyNotFoundException>(()=>NavigationIndexBuilder.Build("s",[]).Resolve("none"));
 [Fact][Trait("Requirement","NAV-06")] public void Build_RejectsAbsoluteArtifactPath()=>Assert.Throws<ArgumentException>(()=>NavigationIndexBuilder.Build("s",[("a","C:/secret.json",0)]));
 [Fact][Trait("Requirement","NAV-07")] public void Build_PreservesSolutionScope()=>Assert.Equal("solution:a",NavigationIndexBuilder.Build("solution:a",[("a","indexes/a.json",0)]).Resolve("a").SolutionKey);
 [Fact][Trait("Requirement","NAV-08")] public void Build_OrdersKeysCanonically()=>Assert.Equal(["a","z"],NavigationIndexBuilder.Build("s",[("z","indexes/z.json",0),("a","indexes/a.json",1)]).Entries.Keys);
 [Fact][Trait("Requirement","NAV-09")] public void Build_RejectsNegativeOrdinal()=>Assert.Throws<ArgumentOutOfRangeException>(()=>NavigationIndexBuilder.Build("s",[("a","indexes/a.json",-1)]));
 [Fact][Trait("Requirement","NAV-10")] public void Resolve_DoesNotDecodeStartKey()=>Assert.Equal(7,NavigationIndexBuilder.Build("s",[("opaque:key","indexes/a.json",7)]).Resolve("opaque:key").Ordinal);
}
