using System.Buffers;

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

        var materialized = records.ToArray();
        return PackCore(
            kind,
            materialized,
            static _ => "{\"entries\":["u8.ToArray(),
            identity: null);
    }

    public ImmutableArray<BoundedUtf8Shard> Pack(
        string kind,
        IReadOnlyList<byte[]> records,
        Func<int, byte[]> prefix,
        Func<int, string> identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(identity);
        if (_maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        return PackCore(
            kind,
            records.Select(static record => (ReadOnlyMemory<byte>)record).ToArray(),
            prefix,
            identity);
    }

    private ImmutableArray<BoundedUtf8Shard> PackCore(
        string kind,
        IReadOnlyList<ReadOnlyMemory<byte>> records,
        Func<int, byte[]> prefix,
        Func<int, string>? identity)
    {
        var result = ImmutableArray.CreateBuilder<BoundedUtf8Shard>();
        SerializedRecordCount += records.Count;
        var first = 0;
        while (first < records.Count)
        {
            var prefixBytes = prefix(first);
            var length = prefixBytes.Length + 3;
            var last = first - 1;
            while (last + 1 < records.Count)
            {
                var candidate = records[last + 1].Length + (last >= first ? 1 : 0);
                if (length + candidate > _maximumBytes) break;
                length += candidate;
                last++;
            }

            if (last < first)
            {
                var recordIdentity = identity is null ? "" : $" '{identity(first)}'";
                throw new InvalidOperationException(
                    $"Compact retrieval {kind} record{recordIdentity} exceeds {_maximumBytes} bytes.");
            }

            result.Add(Create(first, last, prefixBytes, records, length));
            first = last + 1;
        }

        CompletedEnvelopeCount += result.Count;
        return result.ToImmutable();
    }

    private static BoundedUtf8Shard Create(
        int first,
        int last,
        byte[] prefix,
        IReadOnlyList<ReadOnlyMemory<byte>> records,
        int length)
    {
        var buffer = new ArrayBufferWriter<byte>(length);
        buffer.Write(prefix);
        for (var index = first; index <= last; index++)
        {
            if (index != first) buffer.Write(","u8);
            buffer.Write(records[index].Span);
        }

        buffer.Write("]}\n"u8);
        return new BoundedUtf8Shard(first, last, buffer.WrittenMemory.ToArray());
    }
}

internal sealed record BoundedUtf8Shard(int FirstIndex, int LastIndex, byte[] Bytes)
{
    public int Count => LastIndex - FirstIndex + 1;
}

internal sealed record CompactSerializationCounters(
    int RelationRecords,
    int MetadataRecords,
    int PostingLists,
    int PostingOrdinals,
    int CompletedEnvelopes);
