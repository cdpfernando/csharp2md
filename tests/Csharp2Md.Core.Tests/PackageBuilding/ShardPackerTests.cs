using System.Text;
using Csharp2Md.Core.PackageBuilding.Layout;
namespace Csharp2Md.Core.Tests.PackageBuilding;
public sealed class ShardPackerTests
{
    [Theory]
    [InlineData(1)][InlineData(2)][InlineData(3)][InlineData(10)][InlineData(100)][InlineData(1_000)]
    [Trait("Requirement", "STO-06")]
    public void Pack_EmitsOrderedBulkRecords(int size) { var shard=Assert.Single(ShardPacker.Pack("entities", [Record("z",size),Record("a",size)])); Assert.Equal(["a","z"],shard.Records.Select(x=>x.CanonicalKey)); }
    [Theory]
    [InlineData(65_535)][InlineData(65_536)][InlineData(65_537)][InlineData(98_304)]
    [Trait("Requirement", "STO-06")]
    public void Pack_NeverExceedsHardCeilingForAllowedRecord(int size) => Assert.All(ShardPacker.Pack("entities", [Record("a", size)]), shard => Assert.InRange(shard.ByteCount, 0, ShardPacker.HardCeilingBytes));
    [Fact][Trait("Requirement", "EDG-03")] public void Pack_RejectsSingleOversizedRecord() => Assert.Throws<OversizedRecordException>(() => ShardPacker.Pack("entities", [Record("a",ShardPacker.HardCeilingBytes+1)]));
    [Fact][Trait("Requirement", "STO-06")] public void Pack_DoesNotEmitOneFilePerNormalRecord() => Assert.Single(ShardPacker.Pack("entities", [Record("a",100),Record("b",100)]));
    [Fact][Trait("Requirement", "STO-07")] public void Pack_IsPermutationStable() { var a=ShardPacker.Pack("entities",[Record("b",40000),Record("a",40000)]);var b=ShardPacker.Pack("entities",[Record("a",40000),Record("b",40000)]); Assert.Equal(a.Select(x => x.Path),b.Select(x => x.Path)); Assert.Equal(a.SelectMany(x => x.Records).Select(x => x.CanonicalKey),b.SelectMany(x => x.Records).Select(x => x.CanonicalKey)); }
    [Fact][Trait("Requirement", "STO-06")] public void Pack_UsesStableOrdinalPaths() => Assert.Equal(["entities.000000.json","entities.000001.json"],ShardPacker.Pack("entities",[Record("a",40000),Record("b",40000)]).Select(x=>x.Path));
    private static ShardRecord Record(string key,int size) => new(key,Encoding.UTF8.GetBytes(new string('x',size)));
}
