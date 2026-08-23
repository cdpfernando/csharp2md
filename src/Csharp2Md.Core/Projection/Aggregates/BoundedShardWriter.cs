using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Csharp2Md.Core.Projection.Aggregates;

internal sealed class BoundedShardWriter(int maximumBytes = 262144)
{
    public const int DefaultMaximumBytes = 262144;

    private readonly int _maximumBytes = maximumBytes;

    public ImmutableArray<ShardDescriptor> Write(
        string family,
        string key,
        string analysisRunId,
        IEnumerable<RetrievalRelationEntry> entries,
        IAggregateFileWriter files)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisRunId);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(files);
        if (_maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));

        var descriptors = ImmutableArray.CreateBuilder<ShardDescriptor>();
        var current = new List<RetrievalRelationEntry>();
        foreach (var entry in entries.OrderBy(static entry => entry.RelationId, StringComparer.Ordinal))
        {
            var candidate = current.Append(entry).ToImmutableArray();
            if (Serialize(analysisRunId, family, key, candidate).Length <= _maximumBytes)
            {
                current.Add(entry);
                continue;
            }

            if (current.Count == 0)
            {
                throw new InvalidOperationException($"Retrieval index entry exceeds {_maximumBytes} bytes: family '{family}', key '{key}', relation '{entry.RelationId}'.");
            }

            WriteCurrent();
            current.Add(entry);
            if (Serialize(analysisRunId, family, key, current.ToImmutableArray()).Length > _maximumBytes)
            {
                throw new InvalidOperationException($"Retrieval index entry exceeds {_maximumBytes} bytes: family '{family}', key '{key}', relation '{entry.RelationId}'.");
            }
        }

        if (current.Count > 0) WriteCurrent();
        return descriptors.ToImmutable();

        void WriteCurrent()
        {
            var entriesToWrite = current.ToImmutableArray();
            var bytes = Serialize(analysisRunId, family, key, entriesToWrite);
            var path = $"raw/index/shards/{family}/{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))}/{descriptors.Count:D4}.json";
            files.Write(path, bytes);
            descriptors.Add(new ShardDescriptor(family, key, path, entriesToWrite.Length, bytes.Length));
            current.Clear();
        }
    }

    private static byte[] Serialize(string analysisRunId, string family, string key, ImmutableArray<RetrievalRelationEntry> entries) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new RetrievalShard(1, analysisRunId, family, key, entries), AggregateJsonContext.Default.RetrievalShard).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n");
}
