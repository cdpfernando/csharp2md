using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.Core.Tests.Projection.Aggregates;

public sealed class FactualFragmentScannerTests
{
    [Fact]
    public void ScanDocuments_StopsBeforeLargeSourceSections()
    {
        var source = new string('x', 100_000);
        using var stream = new TrackingStream(Json(
            $$"""{"schema_version":6,"documents":[{{Document("doc-1")}}],"source_sections":[{"source":"{{source}}"}]}"""), 127);
        var ids = new List<string>();

        FactualFragmentScanner.ScanDocuments(stream, "raw/facts/doc.json", value =>
            ids.Add(value.GetProperty("document_id").GetString()!));

        Assert.Equal(["doc-1"], ids);
        Assert.True(stream.BytesRead < 4096, $"Scanner read {stream.BytesRead} bytes.");
    }

    [Fact]
    public void ScanRelations_YieldsOneDisposedValueAtATimeAcrossBufferBoundaries()
    {
        var bytes = Json(
            $$"""{"schema_version":6,"documents":[],"relations":[{{Relation("relation-1", new string('x', 10_000))}},{{Relation("relation-2", "target")}}]}""");
        using var stream = new TrackingStream(bytes, 17);
        var ids = new List<string>();
        JsonElement first = default;

        FactualFragmentScanner.ScanRelations(stream, "raw/facts/relations.json", value =>
        {
            ids.Add(value.GetProperty("relation_id").GetString()!);
            if (ids.Count == 1)
            {
                first = value;
            }
            else
            {
                Assert.Throws<ObjectDisposedException>(() => first.GetRawText());
            }
        });

        Assert.Equal(["relation-1", "relation-2"], ids);
        Assert.True(stream.ReadCalls > 2);
    }

    [Theory]
    [InlineData("{\"relations\":[", "malformed JSON")]
    [InlineData("{\"relations\":[],oops}", "malformed JSON")]
    [InlineData("{\"schema_version\":6}", "missing required 'relations' array")]
    [InlineData("{\"relations\":[{\"relation_id\":\"r\"}]}", "Required object 'header' is missing")]
    public void ScanRelations_RejectsInvalidFragmentsWithContext(string json, string expected)
    {
        using var stream = new MemoryStream(Json(json));

        var exception = Assert.Throws<JsonException>(() =>
            FactualFragmentScanner.ScanRelations(stream, "facts/bad.json", static _ => { }));

        Assert.Contains("facts/bad.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains(expected, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanRelations_RejectsSecondNonEmptyRelationArray()
    {
        using var stream = new MemoryStream(Json(
            $$"""{"relations":[{{Relation("relation-1", "one")}}],"relations":[{{Relation("relation-2", "two")}}]}"""));

        var exception = Assert.Throws<JsonException>(() =>
            FactualFragmentScanner.ScanRelations(stream, "facts/duplicate.json", static _ => { }));

        Assert.Contains("facts/duplicate.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("second non-empty 'relations' array", exception.Message, StringComparison.Ordinal);
    }

    private static string Document(string id) =>
        $$"""{"header":{"generated_origin":false},"document_id":"{{id}}","project_id":"project-1","relative_path":"Feature.cs","section_ids":[],"symbol_ids":[]}""";

    private static string Relation(string id, string observed) =>
        $$"""{"header":{"id":"{{id}}","kind":"relation","resolution":"unresolved","provenance":[],"evidence":[],"diagnostic_ids":[]},"relation_id":"{{id}}","source_id":"source-1","partition":"structural","relation_kind":"calls","unresolved_reason":"missing","resolution_method":"unresolved","details":[{"key":"target_text","value":"{{observed}}"}]}""";

    private static byte[] Json(string value) => Encoding.UTF8.GetBytes(value);

    private sealed class TrackingStream(byte[] bytes, int maxRead) : Stream
    {
        private int _position;

        public int BytesRead { get; private set; }
        public int ReadCalls { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ReadCalls++;
            var length = Math.Min(Math.Min(count, maxRead), bytes.Length - _position);
            bytes.AsSpan(_position, length).CopyTo(buffer.AsSpan(offset, length));
            _position += length;
            BytesRead += length;
            return length;
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
