using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.PackageBuilding.Layout;

internal sealed record ShardRecord(string CanonicalKey, ImmutableArray<byte> Payload)
{
    internal ShardRecord(string canonicalKey, ReadOnlySpan<byte> payload) : this(CanonicalText.Require(canonicalKey, nameof(canonicalKey)), payload.ToArray().ToImmutableArray()) { }
}

internal sealed record PackedShard(string Path, ImmutableArray<ShardRecord> Records, int ByteCount);

internal sealed class OversizedRecordException : InvalidOperationException
{
    internal OversizedRecordException(string canonicalKey) : base($"oversized-record: '{canonicalKey}'.") { }
}

internal static class ShardPacker
{
    internal const int TargetBytes = 64 * 1024;
    internal const int HardCeilingBytes = 96 * 1024;

    internal static ImmutableArray<PackedShard> Pack(string family, IEnumerable<ShardRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        family = CanonicalText.Require(family, nameof(family));
        var ordered = records.OrderBy(record => record.CanonicalKey, StringComparer.Ordinal).ToArray();
        var shards = ImmutableArray.CreateBuilder<PackedShard>();
        var current = ImmutableArray.CreateBuilder<ShardRecord>();
        var bytes = 0;
        foreach (var record in ordered)
        {
            if (record.Payload.Length > HardCeilingBytes)
            {
                throw new OversizedRecordException(record.CanonicalKey);
            }

            if (current.Count > 0 && bytes + record.Payload.Length > TargetBytes)
            {
                shards.Add(new PackedShard(Path(family, shards.Count), current.ToImmutable(), bytes));
                current.Clear(); bytes = 0;
            }

            current.Add(record); bytes += record.Payload.Length;
        }

        if (current.Count > 0)
        {
            shards.Add(new PackedShard(Path(family, shards.Count), current.ToImmutable(), bytes));
        }

        return shards.ToImmutable();
    }

    private static string Path(string family, int ordinal) => $"{family}.{ordinal:D6}.json";
}
