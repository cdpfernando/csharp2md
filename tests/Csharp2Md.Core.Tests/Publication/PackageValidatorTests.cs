using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class PackageValidatorTests
{
    [Fact][Trait("Requirement", "PUB-02")] public void Validate_ValidPackageSucceeds() => Assert.True(Validate(Package()).Succeeded);
    [Fact][Trait("Requirement", "PUB-03")] public void Validate_UsesManifestReader() { using var package = Package(); File.Delete(Path.Combine(package.Path, "manifest.json")); Assert.Equal("manifest.json", Assert.Single(Validate(package).Failures).Artifact); }
    [Fact][Trait("Requirement", "PUB-04")] public void Validate_DoesNotMutatePackage() { using var package = Package(); var before = File.ReadAllBytes(Path.Combine(package.Path, "manifest.json")); _ = Validate(package); Assert.Equal(before, File.ReadAllBytes(Path.Combine(package.Path, "manifest.json"))); }
    [Fact][Trait("Requirement", "EDG-05")] public void Validate_MutatedMarkdownReportsArtifact() { using var package = Package(); var path = RootPage(package); File.WriteAllText(Path.Combine(package.Path, path.Replace('/', Path.DirectorySeparatorChar)), "changed\n"); Assert.Equal(path, Assert.Single(Validate(package).Failures).Artifact); }
    [Fact][Trait("Requirement", "PUB-05")] public void Validate_MissingIndexReportsArtifact() { using var package = Package(); var path = Assert.Single(Open(package).Manifest.Solutions).Indexes[0].EntryPath; File.Delete(Path.Combine(package.Path, path.Replace('/', Path.DirectorySeparatorChar))); Assert.Equal(path, Assert.Single(Validate(package).Failures).Artifact); }
    [Theory][Trait("Requirement", "STO-01")][InlineData("manifest.json")][InlineData("certification.json")][InlineData("measurements.json")] public void Validate_CorruptRootArtifactReportsDiagnostic(string path) { using var package = Package(); File.WriteAllText(Path.Combine(package.Path, path), "not-json"); var failure = Assert.Single(Validate(package).Failures); Assert.Equal("package-corruption", failure.Code); Assert.Equal("validation", failure.Stage); }
    [Fact][Trait("Requirement", "STO-03")] public void Validate_MissingManifestReportsCause() { using var package = new TempPackage(); Assert.Equal("missing-artifact", Assert.Single(Validate(package).Failures).Cause); }
    [Fact][Trait("Requirement", "STO-05")] public void Validate_InvalidManifestReportsCause() { using var package = Package(); File.WriteAllText(Path.Combine(package.Path, "manifest.json"), "{}"); Assert.Equal("invalid-package", Assert.Single(Validate(package).Failures).Cause); }
    [Fact][Trait("Requirement", "STO-06")] public void Validate_AbsolutePathPayloadReportsArtifact() { using var package = Package(); var path = Assert.Single(Open(package).Manifest.Solutions).Indexes[0].EntryPath; File.WriteAllText(Path.Combine(package.Path, path.Replace('/', Path.DirectorySeparatorChar)), "C:/secret"); Assert.Equal(path, Assert.Single(Validate(package).Failures).Artifact); }
    [Theory][Trait("Requirement", "PUB-05")][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)] public void Validate_EachMissingDeclaredIndexReportsItsLogicalArtifact(int index) { using var package = Package(); var path = Assert.Single(Open(package).Manifest.Solutions).Indexes[index].EntryPath; File.Delete(Path.Combine(package.Path, path.Replace('/', Path.DirectorySeparatorChar))); Assert.Equal(path, Assert.Single(Validate(package).Failures).Artifact); }

    private static string RootPage(TempPackage package)
    {
        using var reader = Open(package);
        var declared = Assert.Single(reader.Manifest.Solutions).Roots;
        var index = CanonicalJson.Read<RootsIndexData>(reader.ReadArtifact(declared.EntryPath).AsSpan());
        return index.MarkdownPath(index.Roots[0].Handle);
    }

    private static PackageValidationReport Validate(TempPackage package) => PackageValidator.Validate(package.Path);
    private static PackageReader Open(TempPackage package) => PackageReader.Open(package.Path);
    private static TempPackage Package()
    {
        var package = new TempPackage();
        var model = new RetrievalModel([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution("app", "src/App.sln"),
            [new EntityHandle("component:orders")],
            [],
            [])]);
        var machine = MachineArtifactWriter.Write(model, false);
        foreach (var artifact in machine.Artifacts
            .AddRange(MarkdownRenderer.Render(model, machine.Manifest))
            .Add(new PlannedArtifact(new RelativeArtifactPath("certification.json"), ArtifactFamily.Certification, CanonicalJson.Write(new PackageCertification([])), 0, "digest"))
            .Add(new PlannedArtifact(new RelativeArtifactPath("measurements.json"), ArtifactFamily.Measurement, CanonicalJson.Write(new PublicationMeasurements(new ExtractionMeasurements(0, 0), 0, [])), 1, "digest")))
        {
            package.Write(artifact.Path.Value, artifact.Payload);
        }

        return package;
    }
    private sealed class TempPackage : IDisposable { public TempPackage() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csharp2md-validator-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); } public string Path { get; } public void Write(string relative, ImmutableArray<byte> bytes) { var file = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!); File.WriteAllBytes(file, bytes.ToArray()); } public void Dispose() => TempPath.TryDelete(Path); }
}
