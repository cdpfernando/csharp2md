using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.RetrievalIndex.Benchmarks;

namespace Csharp2Md.Core.Tests.Benchmarks;

public sealed class Schema1RetrievalIndexProjectorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Project_MatchesTheFrozenSchemaOneBaseline(bool populated)
    {
        var productionFiles = new RecordingFiles();
        var baselineFiles = new RecordingFiles();
        var json = populated
            ? "{\"schema_version\":6,\"documents\":[],\"relations\":[{\"header\":{\"resolution\":\"exact\",\"evidence\":[]},\"relation_id\":\"relation-a\",\"source_id\":\"source\",\"target_id\":\"target\",\"partition\":\"structural\",\"relation_kind\":\"calls\"}]}"
            : "{\"schema_version\":6,\"documents\":[],\"relations\":[]}";
        var bytes = Encoding.UTF8.GetBytes(json);
        var fragment = new ManifestFragment("fact-1", "facts/relations/all.json", Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.Length);
        productionFiles.Write("raw/facts/relations/all.json", bytes);
        baselineFiles.Write("raw/facts/relations/all.json", bytes);
        var manifest = new FactualManifest(6, "3.0.0", new ManifestAnalysis("full", "full"), "trusted", false, "none", [], new ManifestCoverage(0, 0, 0), [fragment], [fragment.Sha256]);

        new RetrievalIndexProjector().Project(manifest, productionFiles);
        new Schema1RetrievalIndexProjector().Project(manifest, baselineFiles);

        Assert.Equal(productionFiles.Writes.Keys.Order(StringComparer.Ordinal), baselineFiles.Writes.Keys.Order(StringComparer.Ordinal));
        Assert.All(productionFiles.Writes, item => Assert.Equal(item.Value, baselineFiles.Writes[item.Key]));
    }

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        public Dictionary<string, byte[]> Writes { get; } = new(StringComparer.Ordinal);
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes) => Writes[relativePath] = bytes;
        public bool Exists(string relativePath) => Writes.ContainsKey(relativePath);
        public byte[] Read(string relativePath) => Writes[relativePath];
    }
}
