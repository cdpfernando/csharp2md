using System.Security.Cryptography;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Tests.Facts.Storage;

[Trait("Category", "Integration")]
public sealed class FactStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-fact-store-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Persist_ValidatedDocument_WritesOnlyInsideRawFacts()
    {
        var stored = new FactStore(_root).Persist(Fragment("class C { }"));

        var path = Path.Combine(_root, "raw", stored.Reference.Value.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path));
        Assert.StartsWith("facts/document/", stored.Reference.Value, StringComparison.Ordinal);
        Assert.DoesNotContain("..", stored.Reference.Value, StringComparison.Ordinal);
    }

    [Fact]
    public void Persist_HashCoversExactWrittenBytes()
    {
        var stored = new FactStore(_root).Persist(Fragment("class C { string Text = \"aÃ§Ã£o\"; }"));
        var bytes = File.ReadAllBytes(Path.Combine(_root, "raw", stored.Reference.Value.Replace('/', Path.DirectorySeparatorChar)));

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), stored.Sha256);
        Assert.Equal(bytes.Length, stored.ByteLength);
    }

    [Fact]
    public void Persist_LongFactId_UsesFixedLengthPortableHashPath()
    {
        var source = $"class {new string('A', 400)} {{ }}";

        var stored = new FactStore(_root).Persist(Fragment(source, $"src/{new string('p', 120)}.cs"));

        Assert.Equal(64, Path.GetFileNameWithoutExtension(stored.Reference.Value).Length);
        Assert.True(stored.Reference.Value.Length < 120);
    }

    [Fact]
    public void Persist_SameFragmentTwice_ProducesSameReferenceHashAndBytes()
    {
        var store = new FactStore(_root);
        var first = store.Persist(Fragment("class C { }"));
        var firstBytes = Read(first);
        var second = store.Persist(Fragment("class C { }"));

        Assert.Equal(first, second);
        Assert.Equal(firstBytes, Read(second));
    }

    [Fact]
    public void Persist_CanonicalizesFactsRegardlessOfInputOrder()
    {
        var fragment = Fragment("class C { int B; int A; }");
        var reversed = new ValidatedFactFragment(fragment.Facts.Reverse().ToImmutableArray(), fragment.Diagnostics);
        var firstRoot = Path.Combine(_root, "first");
        var secondRoot = Path.Combine(_root, "second");

        var first = new FactStore(firstRoot).Persist(fragment);
        var second = new FactStore(secondRoot).Persist(reversed);

        Assert.Equal(Read(firstRoot, first), Read(secondRoot, second));
        Assert.Equal(first.Sha256, second.Sha256);
    }

    [Fact]
    public void Persist_DistinctIdsWithSameReference_IsRejectedBeforeOverwrite()
    {
        var reference = ArtifactReference.Create(DocumentFactId.Create(
            ProjectFactId.Create("src/App.csproj"), "src/C.cs").ToFactId());
        var store = new FactStore(_root, new FixedReferenceFactory(reference));
        _ = store.Persist(Fragment("class C { }", "src/C.cs"));
        var before = Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories).Single();
        var bytes = File.ReadAllBytes(before);

        Assert.Throws<FactStoreException>(() => store.Persist(Fragment("class D { }", "src/D.cs")));
        Assert.Equal(bytes, File.ReadAllBytes(before));
    }

    [Fact]
    public void Persist_WriteFailureLeavesNoClaimedOrPartialArtifact()
    {
        var files = new FailingMoveOperations();
        var store = new FactStore(_root, files: files);

        Assert.Throws<IOException>(() => store.Persist(Fragment("class C { }")));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.json", SearchOption.AllDirectories));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Persist_BytesAreSourceGeneratedSchemaVersionFiveUtf8Lf()
    {
        var stored = new FactStore(_root).Persist(Fragment("class C { }"));
        var bytes = Read(stored);
        var json = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("\"schema_version\": 5", json, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', json);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
        Assert.False(bytes.AsSpan().StartsWith(System.Text.Encoding.UTF8.Preamble));
    }

    private ValidatedFactFragment Fragment(string source, string relativePath = "src/C.cs")
    {
        var projectId = ProjectFactId.Create("src/App.csproj");
        var extraction = SyntaxFactExtractor.Extract(projectId, relativePath, source);
        return new ValidatedFactFragment(
            [extraction.Document, .. extraction.Document.Sections, .. extraction.Symbols],
            []);
    }

    private byte[] Read(StoredFactFragment stored) => Read(_root, stored);

    private static byte[] Read(string root, StoredFactFragment stored) =>
        File.ReadAllBytes(Path.Combine(root, "raw", stored.Reference.Value.Replace('/', Path.DirectorySeparatorChar)));

    private sealed class FixedReferenceFactory(ArtifactReference reference) : IArtifactReferenceFactory
    {
        public ArtifactReference Create(FactId factId)
        {
            _ = factId;
            return reference;
        }
    }

    private sealed class FailingMoveOperations : IAtomicFileOperations
    {
        public void WriteAllBytes(string path, byte[] bytes) => File.WriteAllBytes(path, bytes);
        public void MoveReplace(string source, string destination) => throw new IOException("Injected move failure.");
        public void DeleteIfExists(string path) => File.Delete(path);
    }
}
