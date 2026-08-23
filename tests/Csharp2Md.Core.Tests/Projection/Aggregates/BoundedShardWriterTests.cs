using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class BoundedShardWriterTests
{
    [Fact]
    public void Write_EmptyInputCreatesNoShardsOrFiles()
    {
        var files = new RecordingFiles();

        var descriptors = new BoundedShardWriter().Write("target", "id1:symbol;name=Status", "run-1", [], files);

        Assert.Empty(descriptors);
        Assert.Empty(files.Writes);
    }

    [Fact]
    public void Write_EntryAtBoundaryIsAcceptedAndOneByteOverflowStartsNextNumberedShard()
    {
        var first = Entry("relation-a");
        var second = Entry("relation-b");
        var sizingFiles = new RecordingFiles();
        var exactBoundary = new BoundedShardWriter(int.MaxValue)
            .Write("target", "id1:symbol;name=Status", "run-1", [first], sizingFiles)
            .Single()
            .ByteLength;
        var oneByteOver = new BoundedShardWriter(int.MaxValue)
            .Write("target", "id1:symbol;name=Status", "run-1", [first, second], new RecordingFiles())
            .Single()
            .ByteLength - 1;
        var exactFiles = new RecordingFiles();
        var files = new RecordingFiles();

        var exactDescriptor = new BoundedShardWriter(exactBoundary)
            .Write("target", "id1:symbol;name=Status", "run-1", [first], exactFiles)
            .Single();
        var descriptors = new BoundedShardWriter(oneByteOver)
            .Write("target", "id1:symbol;name=Status", "run-1", [first, second], files);

        Assert.Equal(exactBoundary, exactDescriptor.ByteLength);
        Assert.Equal(2, descriptors.Length);
        Assert.True(descriptors[0].ByteLength < oneByteOver);
        Assert.Equal("raw/index/shards/target/" + KeyHash("id1:symbol;name=Status") + "/0000.json", descriptors[0].Path);
        Assert.Equal("raw/index/shards/target/" + KeyHash("id1:symbol;name=Status") + "/0001.json", descriptors[1].Path);
        Assert.Equal(new[] { "relation-a" }, RelationIds(files.Writes[descriptors[0].Path]).ToArray());
        Assert.Equal(new[] { "relation-b" }, RelationIds(files.Writes[descriptors[1].Path]).ToArray());
    }

    [Fact]
    public void Write_OversizedSingletonThrowsWithFamilyKeyAndRelationId()
    {
        var entry = Entry("relation-oversized");
        var sizingFiles = new RecordingFiles();
        var length = new BoundedShardWriter(int.MaxValue)
            .Write("resolution", "unresolved", "run-1", [entry], sizingFiles)
            .Single()
            .ByteLength;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new BoundedShardWriter(length - 1).Write("resolution", "unresolved", "run-1", [entry], new RecordingFiles()));

        Assert.Equal(
            $"Retrieval index entry exceeds {length - 1} bytes: family 'resolution', key 'unresolved', relation 'relation-oversized'.",
            exception.Message);
    }

    [Fact]
    public void Write_ReorderedInputProducesByteStableDescriptorsAndCanonicalEntryOrder()
    {
        var firstFiles = new RecordingFiles();
        var secondFiles = new RecordingFiles();
        var entries = new[] { Entry("relation-c"), Entry("relation-a"), Entry("relation-b") };

        var first = new BoundedShardWriter().Write("kind", "calls", "run-1", entries, firstFiles);
        var second = new BoundedShardWriter().Write("kind", "calls", "run-1", entries.Reverse(), secondFiles);

        Assert.Equal(
            first.Select(static descriptor => (descriptor.Family, descriptor.Key, descriptor.Path, descriptor.EntryCount, descriptor.ByteLength)),
            second.Select(static descriptor => (descriptor.Family, descriptor.Key, descriptor.Path, descriptor.EntryCount, descriptor.ByteLength)));
        Assert.Equal(firstFiles.Writes.Keys.Order(StringComparer.Ordinal), secondFiles.Writes.Keys.Order(StringComparer.Ordinal));
        foreach (var path in firstFiles.Writes.Keys)
        {
            Assert.Equal(firstFiles.Writes[path], secondFiles.Writes[path]);
        }

        Assert.Equal(new[] { "relation-a", "relation-b", "relation-c" }, RelationIds(firstFiles.Writes[first[0].Path]).ToArray());
    }

    private static RetrievalRelationEntry Entry(string relationId) => new(
        relationId,
        $"facts/relations/{relationId}.json",
        "id1:symbol;name=Writer",
        "id1:symbol;name=Status",
        "id1:project;name=Orders",
        "structural",
        "writes",
        "exact",
        "exact",
        null,
        null,
        [new RetrievalEvidence("id1:document;name=Orders.cs", "Orders.cs", 1, 1, 1, 10, false, "id1:project;name=Orders", "hash", "3.0.1", null)],
        null);

    private static ImmutableArray<string> RelationIds(byte[] bytes)
    {
        using var json = JsonDocument.Parse(bytes);
        return [.. json.RootElement.GetProperty("entries").EnumerateArray()
            .Select(static entry => entry.GetProperty("relation_id").GetString()!)];
    }

    private static string KeyHash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        public Dictionary<string, byte[]> Writes { get; } = new(StringComparer.Ordinal);
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes) => Writes.Add(relativePath, bytes);
        public bool Exists(string relativePath) => Writes.ContainsKey(relativePath);
        public byte[] Read(string relativePath) => Writes[relativePath];
    }
}
