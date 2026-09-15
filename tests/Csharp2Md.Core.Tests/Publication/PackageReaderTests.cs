using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class PackageReaderTests
{
    [Fact][Trait("Requirement", "NAV-01")] public void Open_ReadsRootManifest() { using var package=Package(); using var reader=PackageReader.Open(package.Path); Assert.Equal(PackageManifest.TokenEstimatorName, reader.Manifest.TokenEstimator); }
    [Fact][Trait("Requirement", "NAV-04")] public void ReadArtifact_ReadsDeclaredIndex() { using var package=Package(); using var reader=PackageReader.Open(package.Path); var path=reader.Manifest.Indexes[0].Path; Assert.NotEmpty(reader.ReadArtifact(path)); }
    [Fact][Trait("Requirement", "NAV-04")] public void ReadDeclaredArtifacts_ContainsManifestAndJourneyEntries() { using var package=Package(); using var reader=PackageReader.Open(package.Path); var artifacts=reader.ReadDeclaredArtifacts(); Assert.Contains("manifest.json", artifacts.Keys); Assert.Contains(reader.Manifest.Journeys[0].EntryPath, artifacts.Keys); }
    [Theory][Trait("Requirement", "PUB-02")][InlineData("/manifest.json")][InlineData("C:/manifest.json")][InlineData("../manifest.json")][InlineData("a/../manifest.json")][InlineData("a\\b.json")] public void ReadArtifact_RejectsUnsafePathBeforeOpen(string path) { using var package=Package(); using var reader=PackageReader.Open(package.Path); Assert.Throws<ArgumentException>(() => reader.ReadArtifact(path)); }
    [Fact][Trait("Requirement", "PUB-05")] public void Open_MissingManifestFails() { using var package=new TempPackage(); Assert.Throws<FileNotFoundException>(() => PackageReader.Open(package.Path)); }
    [Fact][Trait("Requirement", "PUB-05")] public void ReadArtifact_MissingFileFails() { using var package=Package(); using var reader=PackageReader.Open(package.Path); Assert.Throws<FileNotFoundException>(() => reader.ReadArtifact("missing.json")); }
    [Fact][Trait("Requirement", "PUB-03")] public void Open_HoldsSharedLockForWholeView() { using var package=Package(); using var reader=PackageReader.Open(package.Path); Assert.Throws<IOException>(() => new FileStream(Path.Combine(package.Path,"manifest.json"),FileMode.Open,FileAccess.Write,FileShare.None)); }
    [Fact][Trait("Requirement", "PUB-05")] public void Dispose_ReleasesManifestLock() { using var package=Package(); var reader=PackageReader.Open(package.Path); reader.Dispose(); using var writer=new FileStream(Path.Combine(package.Path,"manifest.json"),FileMode.Open,FileAccess.Write,FileShare.None); Assert.True(writer.CanWrite); }
    [Fact][Trait("Requirement", "PUB-02")] public void ReadArtifact_AfterDisposeFails() { using var package=Package(); var reader=PackageReader.Open(package.Path); reader.Dispose(); Assert.Throws<ObjectDisposedException>(() => reader.ReadArtifact("manifest.json")); }
    [Fact][Trait("Requirement", "PUB-05")] public void ReadArtifact_SymlinkEscapeIsRejected() { using var package=Package(); var outside=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json"); File.WriteAllText(outside,"{}"); var link=Path.Combine(package.Path,"escape.json"); try { File.CreateSymbolicLink(link,outside); } catch (IOException) { return; } using var reader=PackageReader.Open(package.Path); Assert.Throws<InvalidOperationException>(() => reader.ReadArtifact("escape.json")); File.Delete(outside); }

    private static TempPackage Package() { var package=new TempPackage(); var model=new RetrievalModel([new SolutionNavigation(CanonicalIdentity.CreateSolution("app","src/App.sln"),[new EntityHandle("component:orders")])],[],[],new NavigationIndexes("identity","roots","outgoing","incoming","contracts","persistence","evidence")); var machine=MachineArtifactWriter.Write(model,false); foreach(var artifact in machine.Artifacts.AddRange(MarkdownRenderer.Render(model,machine.Manifest))) package.Write(artifact.Path.Value,artifact.Payload); return package; }
    private sealed class TempPackage : IDisposable { public TempPackage() { Path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"csharp2md-package-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); } public string Path { get; } public void Write(string relative, ImmutableArray<byte> bytes) { var file=System.IO.Path.Combine(Path,relative.Replace('/',System.IO.Path.DirectorySeparatorChar)); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!); File.WriteAllBytes(file,bytes.ToArray()); } public void Dispose() => TempPath.TryDelete(Path); }
}
