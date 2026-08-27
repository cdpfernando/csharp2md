using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Tests.Filesystem;

namespace Csharp2Md.Storage.Tests.Reading;

[Collection(FilesystemStoreCollection.Name)]
public sealed class UnreadablePackageReadTests
{
    private const string SolutionKey = @"C:\src\Acme Payments.sln";

    [Fact]
    [Trait("Requirement", "STOR-36")]
    [Trait("Requirement", "STOR-37")]
    public void Read_CommittedPackageWithManifestRemoved_NamesNotAPackageAndReturnsNoSnapshot()
    {
        using var output = TempOutputRoot.Create();
        var package = CommitEmpty(output.DirectoryPath);
        var manifest = Path.Combine(package, "manifest.json");
        Assert.True(File.Exists(manifest));
        File.Delete(manifest);
        Assert.False(File.Exists(manifest));

        PackageReadResult? result = null;
        var exception = Assert.Throws<PublicationRejectedException>(() => result = FactualPackageReader.Read(package));

        Assert.Equal("not-a-package", exception.Gate);
        Assert.Contains(package, exception.Detail, StringComparison.Ordinal);
        Assert.Null(result);
    }

    [Fact]
    [Trait("Requirement", "STOR-37")]
    public void Read_RandomDirectory_NamesThePathAndReturnsNoSnapshot()
    {
        using var output = TempOutputRoot.Create();
        var random = Path.Combine(output.DirectoryPath, "not-a-csharp2md-package");
        Directory.CreateDirectory(random);
        File.WriteAllText(Path.Combine(random, "keep-me.txt"), "operator-owned");
        Assert.False(File.Exists(Path.Combine(random, "manifest.json")));

        PackageReadResult? result = null;
        var exception = Assert.Throws<PublicationRejectedException>(() => result = FactualPackageReader.Read(random));

        Assert.Equal("not-a-package", exception.Gate);
        Assert.Contains(random, exception.Detail, StringComparison.Ordinal);
        Assert.Null(result);
    }

    [Fact]
    [Trait("Requirement", "STOR-36")]
    public void Read_IdentityCollisionPackage_NamesTheGateAndReturnsNoSnapshot()
    {
        using var output = TempOutputRoot.Create();
        var package = Path.Combine(output.DirectoryPath, "collision-package");
        var document = DomainMapper.ToWire(
            new FactualSnapshot([Solution.Create(AcmeSolution)], [], [], [], [], []),
            new ManifestContext("s-test", "Acme.sln"));
        var fact = document.Solutions[0];
        var identity = fact.Identity.Id;
        var colliding = document with { Solutions = [fact, fact] };
        PackageDirectoryWriter.Write(package, colliding);

        PackageReadResult? result = null;
        var exception = Assert.Throws<PublicationRejectedException>(() => result = FactualPackageReader.Read(package));

        Assert.Equal("identity-collision", exception.Gate);
        Assert.Contains(identity, exception.Detail, StringComparison.Ordinal);
        Assert.Null(result);
    }

    private static string CommitEmpty(string outputRoot)
    {
        var store = new FilesystemTransactionalStore(outputRoot);
        var session = store.Open(SolutionKey, EmptySourceDocumentReader.Instance);
        session.Stage(FactualSnapshot.Empty);
        session.Commit();
        return FilesystemTestPaths.ChildDirectory(outputRoot, SolutionKey);
    }

    private static WorkspaceIdentity Workspace => WorkspaceIdentity.Create("acme");

    private static SolutionId AcmeSolution => SolutionId.Create(Workspace, "src/Acme.sln");
}
