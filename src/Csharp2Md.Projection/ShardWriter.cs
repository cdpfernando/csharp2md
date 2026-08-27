using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection;

internal static class ShardWriter
{
    public const int DefaultCeilingBytes = 1024 * 1024;

    public static ImmutableArray<StagedFragment> Write(
        string baseKey,
        IReadOnlyList<(string FactId, JsonNode Entry)> entries,
        int ceilingBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseKey);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ceilingBytes);

        if (entries.Count == 0)
        {
            return [];
        }

        var ordered = entries
            .OrderBy(static pair => pair.FactId, StringComparer.Ordinal)
            .ToArray();
        var unsplit = Serialize(ordered.Select(static pair => pair.Entry));
        if (unsplit.Length <= ceilingBytes)
        {
            return [new StagedFragment(ArtifactRole.Payload, baseKey, unsplit)];
        }

        var buckets = new SortedDictionary<string, List<JsonNode>>(StringComparer.Ordinal);
        foreach (var (factId, entry) in ordered)
        {
            var bucket = BucketKey(factId);
            if (!buckets.TryGetValue(bucket, out var items))
            {
                items = [];
                buckets[bucket] = items;
            }

            items.Add(entry);
        }

        var fragments = ImmutableArray.CreateBuilder<StagedFragment>(buckets.Count);
        foreach (var (bucket, items) in buckets)
        {
            fragments.Add(new StagedFragment(
                ArtifactRole.Payload,
                ShardKey(baseKey, bucket),
                Serialize(items)));
        }

        return fragments.ToImmutable();
    }

    internal static string BucketKey(string factId)
    {
        ArgumentException.ThrowIfNullOrEmpty(factId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(factId));
        return Convert.ToHexStringLower(hash.AsSpan(0, 1));
    }

    private static string ShardKey(string baseKey, string bucket)
    {
        var dot = baseKey.LastIndexOf('.');
        return dot < 0
            ? baseKey + "." + bucket
            : string.Concat(baseKey.AsSpan(0, dot), ".", bucket, baseKey.AsSpan(dot));
    }

    private static ImmutableArray<byte> Serialize(IEnumerable<JsonNode> entries)
    {
        var array = new JsonArray();
        foreach (var entry in entries)
        {
            array.Add(entry.DeepClone());
        }

        return CanonicalJson.Write((JsonNode)array);
    }
}
