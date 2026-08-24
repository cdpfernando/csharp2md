using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Projection.Aggregates;
using Csharp2Md.RetrievalIndex.Benchmarks;

namespace Csharp2Md.Core.Tests.Benchmarks;

public sealed class Schema1RetrievalIndexProjectorTests
{
    private static readonly string[] EmptyOracle =
    [
        "raw/index/catalogues/entities.json|aac5ad1c55f98ed7c68a40f82dab21e62a890e9d8cd19b13e6f8cf28f490ef38",
        "raw/index/catalogues/entry-points.json|94be7762d4d9cb873b57b385104e04601062c009c15bb5cf8effa5d9e5b4ac54",
        "raw/index/catalogues/events.json|0d3fa195268677b6d518d6fa28ed7df1431079bfe153f126141a269cc9c23338",
        "raw/index/catalogues/high-centrality.json|f2ec33fc710bfbcbaa345a01d7b8c0e4c272986e44a12b030f915e96ac4723d3",
        "raw/index/catalogues/integrations.json|edc62b8dacaa434a936c33f9ea358f26a52c44d04f426a4a5f17b4a4fe69e540",
        "raw/index/manifest.json|047f8b85aaa11ef3d2869c4d18bafbb605e85de1b4e181c7c168983959d7cb8f",
        "raw/index/summary.json|95a2f629d92299d78b4240fbb7609e2de7eedfe766c0453347c6656feed04322",
        "raw/index/unknowns.json|2fb0887dbcfa6b00d5c3db43a8400e102666645dc8c827c2b16b759192616b84",
    ];

    private static readonly string[] PopulatedOracle =
    [
        "raw/index/catalogues/entities.json|661b0dce91ef028bf97ced30bee1287e9dc5bc631a5ca87ba6e84f0fc8d35eb4",
        "raw/index/catalogues/entry-points.json|94be7762d4d9cb873b57b385104e04601062c009c15bb5cf8effa5d9e5b4ac54",
        "raw/index/catalogues/events.json|0d3fa195268677b6d518d6fa28ed7df1431079bfe153f126141a269cc9c23338",
        "raw/index/catalogues/high-centrality.json|ca2bf1bd9a1bdc2150b9f8216b6295e44a22fbfb8e0f7339042799da94954ead",
        "raw/index/catalogues/integrations.json|edc62b8dacaa434a936c33f9ea358f26a52c44d04f426a4a5f17b4a4fe69e540",
        "raw/index/manifest.json|8de8f87e1fba08f016223cf23593eaf419b631dbc7b38cafdc41c0a00264f22b",
        "raw/index/shards/kind/f46f5990ebfadcab199107258b9dadd8711bd7946d8d00091a1073effcf2a843/0000.json|eab29c1a6117e61adec464ad93b9061b06738f70a3a08567508a3197df56e85a",
        "raw/index/shards/resolution/fa79d4746c21cd960a17b92db8976ddef95a7e20b590721f8e0fa7847a05e486/0000.json|32413c76bdbf9497e87f5b5cddbf00106dd3f9aef5a94b14d8d9727643df13b7",
        "raw/index/shards/source/41cf6794ba4200b839c53531555f0f3998df4cbb01a4d5cb0b94e3ca5e23947d/0000.json|2b33c0021693e4189d1e9f1fdee9806a3c35f5a45d571d5abbe0e46ea2f8aa8d",
        "raw/index/shards/target/34a04005bcaf206eec990bd9637d9fdb6725e0a0c0d4aebf003f17f4c956eb5c/0000.json|2057275aa1dba41704eacc3dca088c154b4d4b83e7c002cf1e7402c1befa2dd4",
        "raw/index/summary.json|47171fe023b314a42e8bbb592e0241e84aa1487789890d3cdc202315a6241ae7",
        "raw/index/unknowns.json|2fb0887dbcfa6b00d5c3db43a8400e102666645dc8c827c2b16b759192616b84",
    ];

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

        var actual = baselineFiles.Writes
            .Where(static item => item.Key.StartsWith("raw/index/", StringComparison.Ordinal))
            .OrderBy(static item => item.Key, StringComparer.Ordinal)
            .Select(static item => $"{item.Key}|{Convert.ToHexStringLower(SHA256.HashData(item.Value))}")
            .ToArray();

        Assert.Equal(populated ? PopulatedOracle : EmptyOracle, actual);
    }

    [Fact]
    public void AllFiveLookups_DeepEqualFrozenSchemaOneLogicalPayloads()
    {
        var schema1Files = new RecordingFiles();
        var schema2Files = new RecordingFiles();
        var documents = Encoding.UTF8.GetBytes(
            """{"schema_version":6,"documents":[{"header":{"generated_origin":false},"document_id":"document-1","project_id":"project-1","relative_path":"First.cs","generated_origin":false},{"header":{"generated_origin":true},"document_id":"document-2","project_id":"project-1","relative_path":"Second.cs","generated_origin":true}],"relations":[]}""");
        var relations = Encoding.UTF8.GetBytes(
            """{"schema_version":6,"documents":[],"relations":[{"header":{"id":"relation-a","resolution":"exact","provenance":[{"engine_version":"3.0.1"}],"evidence":[{"document_id":"document-1","relative_path":"First.cs","start_line":2,"start_column":3,"end_line":4,"end_column":5,"generated_origin":false,"future_evidence":{"rank":1}},{"document_id":"document-2","relative_path":"Second.cs","start_line":6,"start_column":7,"end_line":8,"end_column":9,"generated_origin":true}]},"relation_id":"relation-a","source_id":"source-1","target_id":"target-1","partition":"structural","relation_kind":"calls","resolution_method":"exact","details":[{"key":"target_text","value":"Runtime.Target"},{"key":"hint","value":"preserved"}],"candidates":["candidate-1"],"future_relation":{"version":2}},{"header":{"id":"relation-b","resolution":"unresolved","provenance":[{"engine_version":"3.0.1"}],"evidence":[{"document_id":"document-1","relative_path":"First.cs","start_line":10,"start_column":11,"end_line":12,"end_column":13,"generated_origin":false}]},"relation_id":"relation-b","source_id":"source-1","partition":"structural","relation_kind":"calls","resolution_method":"unresolved","unresolved_reason":"missing","details":[{"key":"target_text","value":"Runtime.Missing"}]}]}""");
        var documentFragment = Fragment("fact-documents", "facts/documents/all.json", documents);
        var relationFragment = Fragment("fact-relations", "facts/relations/all.json", relations);
        foreach (var files in new[] { schema1Files, schema2Files })
        {
            files.Write("raw/facts/documents/all.json", documents);
            files.Write("raw/facts/relations/all.json", relations);
        }

        var manifest = new FactualManifest(
            6,
            "3.0.1",
            new ManifestAnalysis("full", "full"),
            "trusted",
            false,
            "none",
            [],
            new ManifestCoverage(2, 2, 2),
            [documentFragment, relationFragment],
            [documentFragment.Sha256, relationFragment.Sha256]);
        var schema1 = new Schema1RetrievalIndexProjector().Project(manifest, schema1Files).Manifest;
        new RetrievalIndexProjector().Project(manifest, schema2Files);
        var schema2 = RetrievalIndexReader.Open(schema2Files);
        var lookups = new[]
        {
            new RetrievalLookup("project", "project-1"),
            new RetrievalLookup("source", "source-1"),
            new RetrievalLookup("target", "target-1"),
            new RetrievalLookup("kind", "calls"),
            new RetrievalLookup("resolution", "exact"),
        };

        foreach (var lookup in lookups)
        {
            var expected = ReadSchema1(schema1, lookup, schema1Files).Select(Canonicalize).ToArray();
            var actual = schema2.Query(lookup);

            Assert.Equal(expected.Select(static entry => entry.RelationId), actual.Select(static entry => entry.RelationId));
            Assert.Equal(expected.Length, actual.Length);
            for (var index = 0; index < expected.Length; index++) AssertLogicalEqual(expected[index], actual[index]);
        }
    }

    private static ManifestFragment Fragment(string factId, string reference, byte[] bytes) => new(
        factId,
        reference,
        Convert.ToHexStringLower(SHA256.HashData(bytes)),
        bytes.Length);

    private static ImmutableArray<RetrievalRelationEntry> ReadSchema1(
        RetrievalIndexManifest manifest,
        RetrievalLookup lookup,
        RecordingFiles files) => manifest.Shards
            .Where(descriptor => descriptor.Family == lookup.Family && descriptor.Key == lookup.Key)
            .SelectMany(descriptor => JsonSerializer.Deserialize(
                files.Writes[descriptor.Path],
                Schema1JsonContext.Default.RetrievalShard)!.Entries)
            .OrderBy(static entry => entry.RelationId, StringComparer.Ordinal)
            .ToImmutableArray();

    private static RetrievalRelationEntry Canonicalize(RetrievalRelationEntry entry)
    {
        if (entry.Extensions is not { ValueKind: JsonValueKind.Object } extensions) return entry;

        var normalized = JsonNode.Parse(extensions.GetRawText())!.AsObject();
        ImmutableArray<RelationDetailJson>? details = normalized["details"] is JsonArray detailsNode
            ? detailsNode.Select(static detail => new RelationDetailJson(
                detail!["key"]!.GetValue<string>(), detail["value"]!.GetValue<string>())).ToImmutableArray()
            : null;
        ImmutableArray<string>? candidates = normalized["candidates"] is JsonArray candidatesNode
            ? candidatesNode.Select(static candidate => candidate!.GetValue<string>()).ToImmutableArray()
            : null;
        normalized.Remove("details");
        normalized.Remove("candidates");
        return entry with
        {
            Details = details,
            Candidates = candidates,
            Extensions = normalized.Count == 0 ? null : Clone(normalized),
        };
    }

    private static JsonElement Clone(JsonNode node)
    {
        using var document = JsonDocument.Parse(node.ToJsonString());
        return document.RootElement.Clone();
    }

    private static void AssertLogicalEqual(RetrievalRelationEntry expected, RetrievalRelationEntry actual)
    {
        Assert.Equal(expected.RelationId, actual.RelationId);
        Assert.Equal(expected.FragmentReference, actual.FragmentReference);
        Assert.Equal(expected.SourceId, actual.SourceId);
        Assert.Equal(expected.TargetId, actual.TargetId);
        Assert.Equal(expected.ProjectId, actual.ProjectId);
        Assert.Equal(expected.Partition, actual.Partition);
        Assert.Equal(expected.RelationKind, actual.RelationKind);
        Assert.Equal(expected.Resolution, actual.Resolution);
        Assert.Equal(expected.ResolutionMethod, actual.ResolutionMethod);
        Assert.Equal(expected.UnresolvedReason, actual.UnresolvedReason);
        Assert.Equal(expected.ObservedTargetText, actual.ObservedTargetText);
        Assert.Equal(expected.Details, actual.Details);
        Assert.Equal(expected.Candidates, actual.Candidates);
        AssertJsonEqual(expected.Extensions, actual.Extensions);
        Assert.Equal(expected.Evidence.Length, actual.Evidence.Length);
        for (var index = 0; index < expected.Evidence.Length; index++)
        {
            var expectedEvidence = expected.Evidence[index];
            var actualEvidence = actual.Evidence[index];
            Assert.Equal(expectedEvidence.DocumentId, actualEvidence.DocumentId);
            Assert.Equal(expectedEvidence.RelativePath, actualEvidence.RelativePath);
            Assert.Equal(expectedEvidence.StartLine, actualEvidence.StartLine);
            Assert.Equal(expectedEvidence.StartColumn, actualEvidence.StartColumn);
            Assert.Equal(expectedEvidence.EndLine, actualEvidence.EndLine);
            Assert.Equal(expectedEvidence.EndColumn, actualEvidence.EndColumn);
            Assert.Equal(expectedEvidence.GeneratedOrigin, actualEvidence.GeneratedOrigin);
            Assert.Equal(expectedEvidence.ProjectId, actualEvidence.ProjectId);
            Assert.Equal(expectedEvidence.FragmentSha256, actualEvidence.FragmentSha256);
            Assert.Equal(expectedEvidence.GeneratorVersion, actualEvidence.GeneratorVersion);
            AssertJsonEqual(expectedEvidence.Extensions, actualEvidence.Extensions);
        }
    }

    private static void AssertJsonEqual(JsonElement? expected, JsonElement? actual) =>
        Assert.True(
            JsonNode.DeepEquals(
                expected is null ? null : JsonNode.Parse(expected.Value.GetRawText()),
                actual is null ? null : JsonNode.Parse(actual.Value.GetRawText())),
            $"Expected JSON {expected?.GetRawText() ?? "null"}, actual {actual?.GetRawText() ?? "null"}.");

    private sealed class RecordingFiles : IAggregateFileWriter
    {
        public Dictionary<string, byte[]> Writes { get; } = new(StringComparer.Ordinal);
        public void CreateDirectory(string relativePath) { }
        public void Write(string relativePath, byte[] bytes) => Writes[relativePath] = bytes;
        public bool Exists(string relativePath) => Writes.ContainsKey(relativePath);
        public Stream OpenRead(string relativePath) => new MemoryStream(Writes[relativePath], writable: false);
    }
}
