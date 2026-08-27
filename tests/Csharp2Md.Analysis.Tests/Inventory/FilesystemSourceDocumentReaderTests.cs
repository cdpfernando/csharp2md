using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Literals;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class FilesystemSourceDocumentReaderTests
{
    [Fact]
    [Trait("Requirement", "RP-07")]
    public void TryRead_ReadableDocument_ReturnsOriginalBytes()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-src-reader-ok-");
        try
        {
            var path = Path.Combine(tree.FullName, "a.cs");
            ImmutableArray<byte> original = [1, 2, 3, 4];
            File.WriteAllBytes(path, [.. original]);
            var id = DocumentId.Create("doc-a");
            var reader = new FilesystemSourceDocumentReader(tree.FullName, new Dictionary<DocumentId, string> { [id] = path });

            Assert.True(reader.TryRead(id, out var bytes));
            Assert.True(original.AsSpan().SequenceEqual(bytes.AsSpan()));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void TryRead_PathRemovedAfterInventory_ReturnsFalseWithoutThrowing()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-src-reader-gone-");
        try
        {
            var path = Path.Combine(tree.FullName, "gone.cs");
            File.WriteAllBytes(path, [9]);
            var id = DocumentId.Create("doc-gone");
            var reader = new FilesystemSourceDocumentReader(tree.FullName, new Dictionary<DocumentId, string> { [id] = path });
            File.Delete(path);

            var missing = reader.TryRead(id, out var bytes);

            Assert.False(missing);
            Assert.True(bytes.IsDefaultOrEmpty);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void Documents_IsOrderedByDocumentIdOrdinally()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-src-reader-order-");
        try
        {
            var laterPath = Path.Combine(tree.FullName, "z.cs");
            var earlierPath = Path.Combine(tree.FullName, "a.cs");
            File.WriteAllText(laterPath, "z");
            File.WriteAllText(earlierPath, "a");
            var later = DocumentId.Create("zulu");
            var earlier = DocumentId.Create("alpha");
            var reader = new FilesystemSourceDocumentReader(
                tree.FullName,
                new Dictionary<DocumentId, string>
                {
                    [later] = laterPath,
                    [earlier] = earlierPath,
                });

            Assert.Equal(2, reader.Documents.Length);
            Assert.Equal(earlier, reader.Documents[0]);
            Assert.Equal(later, reader.Documents[1]);
            Assert.Equal(
                new[] { earlier.Value, later.Value }.Order(StringComparer.Ordinal),
                reader.Documents.Select(id => id.Value));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void TryRead_UnknownDocument_ReturnsFalse()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-src-reader-unknown-");
        try
        {
            var reader = new FilesystemSourceDocumentReader(tree.FullName, new Dictionary<DocumentId, string>());

            Assert.False(reader.TryRead(DocumentId.Create("missing"), out var bytes));
            Assert.True(bytes.IsDefaultOrEmpty);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void TryRead_DoesNotCacheBytesBetweenCalls()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-src-reader-nocache-");
        try
        {
            var path = Path.Combine(tree.FullName, "changing.cs");
            File.WriteAllBytes(path, [1]);
            var id = DocumentId.Create("doc-change");
            var reader = new FilesystemSourceDocumentReader(tree.FullName, new Dictionary<DocumentId, string> { [id] = path });

            Assert.True(reader.TryRead(id, out var first));
            Assert.True(first.AsSpan().SequenceEqual((ReadOnlySpan<byte>)[1]));
            File.WriteAllBytes(path, [2, 2]);
            Assert.True(reader.TryRead(id, out var second));
            Assert.True(second.AsSpan().SequenceEqual((ReadOnlySpan<byte>)[2, 2]));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
