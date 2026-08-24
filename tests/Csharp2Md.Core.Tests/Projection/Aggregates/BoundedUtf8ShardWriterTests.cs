using System.Text;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class BoundedUtf8ShardWriterTests
{
    [Fact]
    public void Pack_EmptyInputProducesNoShards()
    {
        Assert.Empty(new BoundedUtf8ShardWriter().Pack("relation", []));
    }

    [Fact]
    public void Pack_AcceptsAnExactLimitAndRejectsOneByteOver()
    {
        var record = Encoding.UTF8.GetBytes("{}") as byte[];
        var length = new BoundedUtf8ShardWriter(int.MaxValue).Pack("relation", [record]).Single().Bytes.Length;

        Assert.Equal(length, new BoundedUtf8ShardWriter(length).Pack("relation", [record]).Single().Bytes.Length);
        Assert.Equal($"Compact retrieval relation record exceeds {length - 1} bytes.", Assert.Throws<InvalidOperationException>(
            () => new BoundedUtf8ShardWriter(length - 1).Pack("relation", [record])).Message);
    }

    [Fact]
    public void Pack_SplitsRecordsIntoConsecutiveEnvelopes()
    {
        var record = Encoding.UTF8.GetBytes("{}");
        var one = new BoundedUtf8ShardWriter(int.MaxValue).Pack("posting", [record]).Single().Bytes.Length;
        var shards = new BoundedUtf8ShardWriter(one).Pack("posting", [record, record]);

        Assert.Equal(2, shards.Length);
        Assert.All(shards, shard => Assert.Equal(one, shard.Bytes.Length));
    }
}
