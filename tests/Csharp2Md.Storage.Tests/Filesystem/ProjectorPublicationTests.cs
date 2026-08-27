using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class ProjectorPublicationTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";
    private const string FragmentKey = "projections/sample.json";

    private static readonly ImmutableArray<byte> FragmentBytes =
        Encoding.UTF8.GetBytes("{\"ok\":true}").ToImmutableArray();

    [Fact]
    [Trait("Requirement", "RP-03")]
    public void Commit_ProjectorReturningOneFragment_WritesFragmentToDisk()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, new OneFragmentProjector());
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var onDisk = FilesystemTestPaths.SnapshotFiles(child);
        Assert.True(onDisk.ContainsKey(FragmentKey), $"Expected '{FragmentKey}' on disk.");
        Assert.True(FragmentBytes.AsSpan().SequenceEqual(onDisk[FragmentKey]));
    }

    [Fact]
    [Trait("Requirement", "RP-03")]
    public void Commit_ProjectorReturningOneFragment_ListsFragmentInManifest()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, new OneFragmentProjector());
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        var manifest = CanonicalJson.Read<ManifestEnvelope>(
            FilesystemTestPaths.SnapshotFiles(child)["manifest.json"]);
        var entry = Assert.Single(manifest.Artifacts, candidate => candidate.Path == FragmentKey);
        Assert.Equal("projections/sample", entry.CanonicalKey);
        Assert.Equal("payload", entry.Role);
    }

    [Fact]
    [Trait("Requirement", "RP-03")]
    public void Commit_Projector_ReceivesPostValidationDocument()
    {
        using var output = TempOutputRoot.Create();
        var projector = new CapturingProjector();
        var store = new FilesystemTransactionalStore(output.DirectoryPath, projector);
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();

        Assert.NotNull(projector.View);
        var context = new ManifestContext(
            SolutionCoordinate.For(SolutionKey).Identity.Value,
            Path.GetFileName(SolutionKey));
        var expected = PackageValidator.Validate(DomainMapper.ToWire(FactualSnapshot.Empty, context)).Document;
        Assert.Equal(expected.Manifest.SolutionKey, projector.View.Document.Manifest.SolutionKey);
        Assert.Equal(expected.RunCertification, projector.View.Document.RunCertification);
        Assert.Equal(expected.Quarantine.Length, projector.View.Document.Quarantine.Length);
    }

    [Fact]
    [Trait("Requirement", "RP-02")]
    [Trait("Requirement", "RP-03")]
    public void PublicationPipeline_ProjectsFromValidatedDocumentBeforeOrdering()
    {
        var pipeline = File.ReadAllText(
            Path.Combine(StorageTestPaths.RepoRoot, "src", "Csharp2Md.Storage", "Mapping", "PublicationPipeline.cs"));

        var validate = pipeline.IndexOf("PackageValidator.Validate", StringComparison.Ordinal);
        var viewFromReport = pipeline.IndexOf("PublishedPackageView.From(report.Document)", StringComparison.Ordinal);
        var project = pipeline.IndexOf("projector.Project(view, source)", StringComparison.Ordinal);
        var order = pipeline.IndexOf("PackagePublisher.ToPublicationOrder", StringComparison.Ordinal);

        Assert.True(validate >= 0, "Pipeline must validate before projecting.");
        Assert.True(viewFromReport > validate, "Projector view must be built from the post-validation document.");
        Assert.True(project > viewFromReport, "Project must run after the post-validation view is built.");
        Assert.True(order > project, "Ordering must run after projection.");
        Assert.DoesNotContain("PublishedPackageView.From(document)", pipeline, StringComparison.Ordinal);
    }

    private sealed class OneFragmentProjector : IPackageProjector
    {
        public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(source);
            return [new StagedFragment(ArtifactRole.Payload, FragmentKey, FragmentBytes)];
        }
    }

    private sealed class CapturingProjector : IPackageProjector
    {
        public PublishedPackageView? View { get; private set; }

        public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
        {
            View = view;
            return [];
        }
    }
}
