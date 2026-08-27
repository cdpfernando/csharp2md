using System.Text;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Storage.Mapping;

namespace Csharp2Md.Storage.Tests.Filesystem;

[Collection(FilesystemStoreCollection.Name)]
public sealed class ProjectionValidationAbortTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "RP-41")]
    [Trait("Requirement", "RP-42")]
    public void Commit_MissingProjectionKey_LeavesNoStaging()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(
            output.DirectoryPath,
            new CitationProjector("facts/absent.json", 0));
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        var exception = Assert.Throws<PublicationRejectedException>(session.Commit);

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        Assert.Equal("projection-key", exception.Gate);
        Assert.Equal("facts/absent.json", exception.Detail);
        Assert.False(Directory.Exists(child + ".staging"));
        Assert.False(File.Exists(Path.Combine(child + ".staging", "manifest.json")));
    }

    [Fact]
    [Trait("Requirement", "RP-41")]
    [Trait("Requirement", "RP-43")]
    public void Commit_OutOfRangeProjectionOrdinal_LeavesNoStaging()
    {
        using var output = TempOutputRoot.Create();
        var store = new FilesystemTransactionalStore(
            output.DirectoryPath,
            new CitationProjector("coverage.json", 1));
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        var exception = Assert.Throws<PublicationRejectedException>(session.Commit);

        var child = FilesystemTestPaths.ChildDirectory(output.DirectoryPath, SolutionKey);
        Assert.Equal("projection-ordinal", exception.Gate);
        Assert.Contains("coverage.json", exception.Detail, StringComparison.Ordinal);
        Assert.Contains("1", exception.Detail, StringComparison.Ordinal);
        Assert.False(Directory.Exists(child + ".staging"));
        Assert.False(File.Exists(Path.Combine(child + ".staging", "manifest.json")));
    }

    private sealed class CitationProjector : IPackageProjector
    {
        private readonly string _artifactKey;
        private readonly int _ordinal;

        public CitationProjector(string artifactKey, int ordinal)
        {
            _artifactKey = artifactKey;
            _ordinal = ordinal;
        }

        public ImmutableArray<StagedFragment> Project(PublishedPackageView view, ISourceDocumentReader source)
        {
            ArgumentNullException.ThrowIfNull(view);
            ArgumentNullException.ThrowIfNull(source);
            var json = $$"""{"artifact_key":"{{_artifactKey}}","ordinal":{{_ordinal}}}""";
            return [new StagedFragment(
                ArtifactRole.Payload,
                "projections/cite.json",
                Encoding.UTF8.GetBytes(json).ToImmutableArray())];
        }
    }
}
