using System.Buffers;
using System.Text;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed class BoundedUtf8ShardWriter(int maximumBytes = 262144)
{
    public const int DefaultMaximumBytes = 262144;
    private readonly int _maximumBytes = maximumBytes;
    public int SerializedRecordCount { get; private set; }
    public int CompletedEnvelopeCount { get; private set; }

    public ImmutableArray<BoundedUtf8Shard> Pack(string kind, IEnumerable<ReadOnlyMemory<byte>> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(records);
        if (_maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        var result = ImmutableArray.CreateBuilder<BoundedUtf8Shard>();
        var current = new List<ReadOnlyMemory<byte>>();
        foreach (var record in records)
        {
            SerializedRecordCount++;
            if (Length(current, record) > _maximumBytes)
            {
                if (current.Count == 0)
                {
                    throw new InvalidOperationException($"Compact retrieval {kind} record exceeds {_maximumBytes} bytes.");
                }

                result.Add(Create(current));
                current.Clear();
                if (Length(current, record) > _maximumBytes)
                {
                    throw new InvalidOperationException($"Compact retrieval {kind} record exceeds {_maximumBytes} bytes.");
                }
            }

            current.Add(record);
        }

        if (current.Count != 0) result.Add(Create(current));
        CompletedEnvelopeCount += result.Count;
        return result.ToImmutable();
    }

    private static int Length(List<ReadOnlyMemory<byte>> records, ReadOnlyMemory<byte> next) =>
        12 + records.Sum(static item => item.Length) + next.Length + records.Count + 3;

    private static BoundedUtf8Shard Create(List<ReadOnlyMemory<byte>> records)
    {
        var buffer = new ArrayBufferWriter<byte>();
        buffer.Write("{\"entries\":["u8);
        for (var index = 0; index < records.Count; index++)
        {
            if (index != 0) buffer.Write(","u8);
            buffer.Write(records[index].Span);
        }
        buffer.Write("]}\n"u8);
        return new BoundedUtf8Shard(buffer.WrittenMemory.ToArray());
    }
}

internal sealed record BoundedUtf8Shard(byte[] Bytes);

internal sealed record CompactShardDescriptor(string Path, string Kind, string? Key, int EntryCount, int ByteLength);
internal sealed record CompactSerializationCounters(
    int RelationRecords,
    int MetadataRecords,
    int PostingLists,
    int PostingOrdinals,
    int CompletedEnvelopes);

internal sealed class CompactShardPolicy(string kind, string directory)
{
    public string Kind { get; } = kind;
    public string Directory { get; } = directory;

    public ImmutableArray<CompactShardDescriptor> Write(
        BoundedUtf8ShardWriter writer,
        IEnumerable<ReadOnlyMemory<byte>> records,
        IAggregateFileWriter files)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(files);
        var shards = writer.Pack(Kind, records);
        return shards.Select((shard, ordinal) =>
        {
            var path = $"raw/index/{Directory}/{ordinal:D4}.json";
            files.Write(path, shard.Bytes);
            return new CompactShardDescriptor(path, Kind, null, 0, shard.Bytes.Length);
        }).ToImmutableArray();
    }
}

internal static class CompactShardPolicies
{
    public static CompactShardPolicy Relations { get; } = new("relation", "relations");
    public static CompactShardPolicy Metadata { get; } = new("metadata", "metadata");
    public static CompactShardPolicy Postings { get; } = new("posting", "postings");
}

internal sealed class CompactPostingShardPolicy
{
    public ImmutableArray<CompactShardDescriptor> Write(
        string family,
        string key,
        IEnumerable<ReadOnlyMemory<byte>> postingSegments,
        BoundedUtf8ShardWriter writer,
        IAggregateFileWriter files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var shards = writer.Pack("posting", postingSegments);
        return shards.Select((shard, ordinal) =>
        {
            var path = $"raw/index/postings/{ordinal:D4}.json";
            files.Write(path, shard.Bytes);
            return new CompactShardDescriptor(path, "posting", key, 0, shard.Bytes.Length);
        }).ToImmutableArray();
    }
}
