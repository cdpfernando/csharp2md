using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Projection.Source;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Tests.Source;

public sealed class SourceProjectorTests
{
    private static readonly ManifestContext Context = new("s-test", "Acme.sln");

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void Project_InventoriedDocuments_EmitsExactlyOneArtifactEach()
    {
        var (view, reader, documents) = PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", "class Program;"u8.ToArray()),
            ("Acme.Payments/Acme.Payments.csproj", "Acme.Payments/Program.cs", "class Payments;"u8.ToArray()));

        var fragments = SourceProjector.Project(view, reader);

        Assert.Equal(documents.Length, fragments.Length);
        Assert.All(fragments, fragment => Assert.StartsWith("source/", fragment.CanonicalKey, StringComparison.Ordinal));
        Assert.Equal(fragments.Length, fragments.Select(fragment => fragment.CanonicalKey).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    [Trait("Requirement", "RP-08")]
    public void Project_ArtifactKeys_ContainNoAbsoluteOrCloneDependentSegment()
    {
        var cloneRoot = OperatingSystem.IsWindows() ? @"D:\clones\acme" : "/tmp/clones/acme";
        var (view, reader, _) = PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Program.cs", "class Program;"u8.ToArray()));

        var fragments = SourceProjector.Project(view, reader);

        var key = Assert.Single(fragments).CanonicalKey;
        Assert.False(Path.IsPathRooted(key), key);
        Assert.DoesNotContain(cloneRoot, key, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(":", key, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", key, StringComparison.Ordinal);
        Assert.StartsWith("source/", key, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-08")]
    public void Project_SharedRelativePathInDifferentProjects_ProducesDistinctKeys()
    {
        const string relative = "src/Program.cs";
        var (view, reader, _) = PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", relative, "orders"u8.ToArray()),
            ("Acme.Payments/Acme.Payments.csproj", relative, "payments"u8.ToArray()));

        var fragments = SourceProjector.Project(view, reader);

        Assert.Equal(2, fragments.Length);
        Assert.All(fragments, fragment => Assert.Contains(relative, fragment.CanonicalKey, StringComparison.Ordinal));
        Assert.NotEqual(fragments[0].CanonicalKey, fragments[1].CanonicalKey);
        Assert.Contains(fragments, fragment => fragment.CanonicalKey.Contains("acme.orders", StringComparison.Ordinal));
        Assert.Contains(fragments, fragment => fragment.CanonicalKey.Contains("acme.payments", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void Project_NonUtf8Bytes_PassThroughUnchanged()
    {
        var opaque = new byte[] { 0x00, 0xFF, 0xFE, 0x80, 0x7F };
        var (view, reader, _) = PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/blob.bin", opaque));

        var fragments = SourceProjector.Project(view, reader);

        var fragment = Assert.Single(fragments);
        Assert.True(fragment.IsDeferred);
        Assert.True(opaque.AsSpan().SequenceEqual(fragment.ReadPayload().AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    public void Project_ReadableDocument_DefersPayloadUntilRead()
    {
        var utf8 = Encoding.UTF8.GetBytes("namespace Acme;");
        var (view, reader, _) = PackageWith(
            ("Acme.Orders/Acme.Orders.csproj", "Acme.Orders/Code.cs", utf8));

        var fragment = Assert.Single(SourceProjector.Project(view, reader));

        Assert.True(fragment.IsDeferred);
        Assert.True(fragment.Payload.IsDefaultOrEmpty);
        Assert.True(utf8.AsSpan().SequenceEqual(fragment.ReadPayload().AsSpan()));
    }

    [Fact]
    [Trait("Requirement", "RP-07")]
    [Trait("Requirement", "RP-08")]
    public void PackageProjector_ComposesSourceProjectorInCanonicalOrder()
    {
        var (view, reader, _) = PackageWith(
            ("Zeta/Zeta.csproj", "Zeta/z.cs", "z"u8.ToArray()),
            ("Alpha/Alpha.csproj", "Alpha/a.cs", "a"u8.ToArray()));

        var composed = new PackageProjector().Project(view, reader);
        var direct = SourceProjector.Project(view, reader);
        var ordered = view.Document.Documents
            .OrderBy(static dto => dto.Identity.Id, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            direct.Select(fragment => fragment.CanonicalKey),
            composed.Select(fragment => fragment.CanonicalKey));
        Assert.Equal(ordered.Length, composed.Length);
        for (var i = 0; i < ordered.Length; i++)
        {
            Assert.Contains(ordered[i].RelativePath, composed[i].CanonicalKey, StringComparison.Ordinal);
        }
    }

    private static (PublishedPackageView View, ISourceDocumentReader Reader, ImmutableArray<Document> Documents) PackageWith(
        params (string ProjectPath, string RelativePath, byte[] Bytes)[] files)
    {
        var workspace = WorkspaceIdentity.Create("acme");
        var solutionId = SolutionId.Create(workspace, "src/Acme.sln");
        var facts = ImmutableArray.CreateBuilder<IFact>();
        facts.Add(Solution.Create(solutionId));

        var bytesById = ImmutableDictionary.CreateBuilder<DocumentId, ImmutableArray<byte>>();
        var documents = ImmutableArray.CreateBuilder<Document>();
        foreach (var (projectPath, relativePath, bytes) in files)
        {
            var projectId = ProjectId.Create(solutionId, projectPath);
            facts.Add(Project.Create(projectId));
            var document = Document.Create(projectId, relativePath);
            facts.Add(document);
            documents.Add(document);
            bytesById[DocumentId.Create(document.Reference.Id.Value)] = [.. bytes];
        }

        var view = PublishedPackageView.From(
            DomainMapper.ToWire(new FactualSnapshot(facts.ToImmutable(), [], [], [], [], []), Context));
        return (view, new DictionarySourceReader(bytesById.ToImmutable()), documents.ToImmutable());
    }

    private sealed class DictionarySourceReader : ISourceDocumentReader
    {
        private readonly ImmutableDictionary<DocumentId, ImmutableArray<byte>> _bytes;

        public DictionarySourceReader(ImmutableDictionary<DocumentId, ImmutableArray<byte>> bytes)
        {
            _bytes = bytes;
            Documents = [.. bytes.Keys.OrderBy(static id => id.Value, StringComparer.Ordinal)];
        }

        public ImmutableArray<DocumentId> Documents { get; }

        public bool TryRead(DocumentId document, out ImmutableArray<byte> bytes) =>
            _bytes.TryGetValue(document, out bytes);
    }
}
