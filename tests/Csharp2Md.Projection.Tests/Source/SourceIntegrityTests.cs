using System.Security.Cryptography;
using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection.Source;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Projection.Tests.Source;

public sealed class SourceIntegrityTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "RP-11")]
    public void Project_UnredactedDocument_PublishedSha256EqualsContentSha256()
    {
        var body = Encoding.UTF8.GetBytes("class Program;");
        var (view, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", body));

        var fragment = Assert.Single(SourceProjector.Project(view, reader));
        var published = fragment.ReadPayload();
        var digest = Convert.ToHexStringLower(SHA256.HashData(published.AsSpan()));

        Assert.Equal(view.Document.Documents[0].ContentSha256, digest);
        Assert.True(body.AsSpan().SequenceEqual(published.AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "RP-11")]
    public void Project_BytesChangedAfterInventory_AbortsSourceDriftNamingTheDocument()
    {
        var original = Encoding.UTF8.GetBytes("class Program;");
        var (view, _, documents) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", original));
        var document = Assert.Single(documents);
        var identity = DocumentId.Create(document.Reference.Id.Value);
        var reader = new MutableSourceReader(identity, Encoding.UTF8.GetBytes("class Drifted;"));

        var fragment = Assert.Single(SourceProjector.Project(view, reader));
        var exception = Assert.Throws<PublicationRejectedException>(() => fragment.ReadPayload());

        Assert.Equal("source-drift", exception.Gate);
        Assert.Equal(document.Reference.Id.Value, exception.Detail);
        Assert.Contains(document.Reference.Id.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-11")]
    public void Project_MatchingBytes_DoNotAbort()
    {
        var original = Encoding.UTF8.GetBytes("class Program;");
        var (view, reader, _) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", original));

        var fragment = Assert.Single(SourceProjector.Project(view, reader));

        Assert.True(original.AsSpan().SequenceEqual(fragment.ReadPayload().AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "RP-11")]
    public void Commit_SourceDrift_LeavesPriorPackageByteIdentical()
    {
        var original = Encoding.UTF8.GetBytes("class Program;");
        var (view, matchingReader, documents) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", original));
        var document = Assert.Single(documents);
        var identity = DocumentId.Create(document.Reference.Id.Value);
        var snapshot = DomainMapper.FromWire(view.Document);
        var output = Directory.CreateTempSubdirectory("csharp2md-source-drift-");
        try
        {
            var store = new FilesystemTransactionalStore(output.FullName, new PackageProjector());
            var first = store.Open(SolutionKey, matchingReader);
            first.Stage(snapshot);
            first.Commit();
            var child = ChildDirectory(output.FullName, SolutionKey);
            var prior = SnapshotFiles(child);

            var drifted = store.Open(SolutionKey, new MutableSourceReader(identity, Encoding.UTF8.GetBytes("class Drifted;")));
            drifted.Stage(snapshot);
            var exception = Assert.Throws<PublicationRejectedException>(drifted.Commit);
            var after = SnapshotFiles(child);

            Assert.Equal("source-drift", exception.Gate);
            Assert.Equal(document.Reference.Id.Value, exception.Detail);
            Assert.Equal(prior.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
            foreach (var key in prior.Keys)
            {
                Assert.True(prior[key].AsSpan().SequenceEqual(after[key]), $"Bytes at '{key}' changed.");
            }
        }
        finally
        {
            output.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "RP-11")]
    public void Project_RedactedDocument_OriginalHashStillMatchesContentSha256()
    {
        var body = "token=hunter2-value";
        var (view, reader, _) = ProjectionPackageFactory.PackageWithSecret(
            "Acme.Orders/Acme.Orders.csproj",
            "Acme.Orders/appsettings.json",
            body,
            new Csharp2Md.Storage.Wire.SourceSpanDto(1, 7, 1, body.Length));

        var fragments = SourceProjector.Project(view, reader);
        var source = Assert.Single(
            fragments,
            fragment => !fragment.CanonicalKey.EndsWith(".meta.json", StringComparison.Ordinal));
        var envelope = Csharp2Md.Storage.Wire.CanonicalJson.Read<Csharp2Md.Storage.Wire.RedactionEnvelopeDto>(
            Assert.Single(
                    fragments,
                    fragment => fragment.CanonicalKey.EndsWith(".meta.json", StringComparison.Ordinal))
                .ReadPayload()
                .AsSpan());
        _ = source.ReadPayload();

        Assert.Equal(view.Document.Documents[0].ContentSha256, envelope.OriginalSha256);
        Assert.NotEqual(view.Document.Documents[0].ContentSha256, envelope.PublishedSha256);
    }

    [Fact]
    [Trait("Requirement", "RP-11")]
    public void Project_UnreadableDocument_OmitsSourceBytesWithoutSourceDrift()
    {
        var original = Encoding.UTF8.GetBytes("class Program;");
        var (view, _, documents) = ProjectionPackageFactory.PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", original));
        var identity = DocumentId.Create(Assert.Single(documents).Reference.Id.Value);
        var reader = new MutableSourceReader(identity, original, readable: false);

        var fragment = Assert.Single(SourceProjector.Project(view, reader));

        Assert.True(fragment.ReadPayload().IsDefaultOrEmpty);
    }

    private static string ChildDirectory(string outputRoot, string solutionKey)
    {
        var hex = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(solutionKey)))[..32];
        return Path.Combine(outputRoot, "s-" + hex);
    }

    private static IReadOnlyDictionary<string, byte[]> SnapshotFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(directory, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);
    }

    private sealed class MutableSourceReader : ISourceDocumentReader
    {
        private readonly DocumentId _id;
        private readonly ImmutableArray<byte> _bytes;
        private readonly bool _readable;

        public MutableSourceReader(DocumentId id, byte[] bytes, bool readable = true)
        {
            _id = id;
            _bytes = [.. bytes];
            _readable = readable;
            Documents = [_id];
        }

        public ImmutableArray<DocumentId> Documents { get; }

        public bool TryRead(DocumentId document, out ImmutableArray<byte> bytes)
        {
            if (!_readable || !document.Equals(_id))
            {
                bytes = default;
                return false;
            }

            bytes = _bytes;
            return true;
        }
    }
}
