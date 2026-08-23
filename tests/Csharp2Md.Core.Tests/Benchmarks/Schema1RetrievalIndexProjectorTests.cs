using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.RetrievalIndex.Benchmarks;

namespace Csharp2Md.Core.Tests.Benchmarks;

public sealed class Schema1RetrievalIndexProjectorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Project_FrozenSchemaOneBaselineKeepsItsPhysicalContract(bool populated)
    {
        var baselineFiles = new RecordingFiles();
        var json = populated
            ? "{\"schema_version\":6,\"documents\":[],\"relations\":[{\"header\":{\"id\":\"relation-a\",\"resolution\":\"exact\",\"evidence\":[]},\"relation_id\":\"relation-a\",\"source_id\":\"source\",\"target_id\":\"target\",\"partition\":\"structural\",\"relation_kind\":\"calls\"}]}"
            : "{\"schema_version\":6,\"documents\":[],\"relations\":[]}";
        var bytes = Encoding.UTF8.GetBytes(json);
        var fragment = new ManifestFragment("fact-1", "facts/relations/all.json", Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length);
        baselineFiles.Write("raw/facts/relations/all.json", bytes);
        var manifest = new FactualManifest(6, "3.0.0", new ManifestAnalysis("full", "full"), "trusted", false, "none", [], new ManifestCoverage(0, 0, 0), [fragment], [fragment.Sha256]);

        new Schema1RetrievalIndexProjector().Project(manifest, baselineFiles);

        using var indexManifest = JsonDocument.Parse(baselineFiles.Writes["raw/index/manifest.json"]);
        Assert.Equal(1, indexManifest.RootElement.GetProperty("schema_version").GetInt32());
        Assert.All(new[] { "entities", "events", "integrations", "entry-points", "high-centrality" },
            name => Assert.True(baselineFiles.Writes.ContainsKey($"raw/index/catalogues/{name}.json")));
        Assert.Equal(populated ? 4 : 0, indexManifest.RootElement.GetProperty("shards").GetArrayLength());
    }

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        public Dictionary<string, byte[]> Writes { get; } = new(StringComparer.Ordinal);
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes) => Writes[relativePath] = bytes;
        public bool Exists(string relativePath) => Writes.ContainsKey(relativePath);
        public Stream OpenRead(string relativePath) => new MemoryStream(Writes[relativePath], writable: false);
    }
}
