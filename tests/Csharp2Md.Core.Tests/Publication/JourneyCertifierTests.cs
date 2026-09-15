using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;
using Csharp2Md.Core.Publication.Certification;
using Csharp2Md.Core.Analysis;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class JourneyCertifierTests
{
    [Fact][Trait("Requirement", "NAV-06")] public void Certify_LocateComponentPassesWithinFiveReads() => Assert.Equal(JourneyCertificationStatus.Passed, Locate(Package()).Status);
    [Fact][Trait("Requirement", "NAV-07")] public void Certify_LocateOtherRootPassesWithinEightReads() { using var package = Package("service:orders"); Assert.Equal(JourneyCertificationStatus.Passed, Locate(package).Status); }
    [Fact][Trait("Requirement", "NAV-10")] public void Certify_EvidencePassesWithinBudget() { using var package = Package(); Assert.Equal(JourneyCertificationStatus.Passed, Evidence(package).Status); }
    [Fact][Trait("Requirement", "CRT-02")] public void Certify_EmptyRootsIsNotApplicable() { using var package = Package(roots: []); var journey = JourneyCertifier.Certify(package.Path).Journeys.Single(x => x.Kind == JourneyKind.Locate); Assert.Equal(JourneyCertificationStatus.NotApplicable, journey.Status); Assert.StartsWith("not_applicable:", journey.Detail); }
    [Fact][Trait("Requirement", "CRT-02")] public void Certify_GraphJourneysAreNotApplicableUntilTheirCertifierRuns() { using var package = Package(); Assert.All(JourneyCertifier.Certify(package.Path).Journeys.Where(x => x.Kind is JourneyKind.FollowFlow or JourneyKind.ReverseImpact), x => Assert.Equal(JourneyCertificationStatus.NotApplicable, x.Status)); }
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_MissingLocateTerminalFails() { using var package = Package(); File.Delete(Directory.GetFiles(package.Path, "*.md", SearchOption.AllDirectories).Single(x => x.Contains("components", StringComparison.Ordinal))); Assert.Equal(JourneyCertificationStatus.Failed, Locate(package).Status); }
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_MissingEvidenceTerminalFails() { using var package = Package(); using var reader = PackageReader.Open(package.Path); var evidence = reader.Manifest.Journeys.Single(x => x.Kind == JourneyKind.EvidenceDisposition).EntryPath; File.Delete(Path.Combine(package.Path, evidence.Replace('/', Path.DirectorySeparatorChar))); Assert.Equal(JourneyCertificationStatus.Failed, Evidence(package).Status); }
    [Fact][Trait("Requirement", "EDG-02")] public void Certify_LargeComponentPageFailsTokenBudget() { using var package = Package(); var markdown = Directory.GetFiles(package.Path, "*.md", SearchOption.AllDirectories).Single(x => x.Contains("components", StringComparison.Ordinal)); File.WriteAllText(markdown, new string('x', 50_000)); Assert.Contains("tokens-exceeded", Locate(package).Detail); }
    [Fact][Trait("Requirement", "NAV-06")] public void MeasuredReader_RecordsOneReadPerPath() { using var package = Package(); using var reader = MeasuredPackageReader.Open(package.Path); reader.OpenArtifact("manifest.json"); reader.OpenArtifact("manifest.json"); Assert.Equal(1, reader.Measurement.Reads); }
    [Fact][Trait("Requirement", "NAV-10")] public void MeasuredReader_UsesCeilingForTokens() { using var package = Package(); using var reader = MeasuredPackageReader.Open(package.Path); reader.OpenArtifact("manifest.json"); Assert.Equal((long)Math.Ceiling(reader.Measurement.Bytes / 4.0), reader.Measurement.Tokens); }
    [Fact][Trait("Requirement", "NAV-06")] public void Certify_LocateDetailIncludesMeasurements() { using var package = Package(); Assert.Contains("reads:", Locate(package).Detail); }
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_ApplicableJourneyNeverReportsNotApplicable() { using var package = Package(); Assert.NotEqual(JourneyCertificationStatus.NotApplicable, Locate(package).Status); }

    private static JourneyCertification Locate(TempPackage package) => JourneyCertifier.Certify(package.Path).Journeys.Single(x => x.Kind == JourneyKind.Locate);
    private static JourneyCertification Evidence(TempPackage package) => JourneyCertifier.Certify(package.Path).Journeys.Single(x => x.Kind == JourneyKind.EvidenceDisposition);
    private static TempPackage Package(string root = "component:orders", ImmutableArray<EntityHandle> roots = default)
    {
        var package = new TempPackage();
        var actualRoots = roots.IsDefault ? ImmutableArray.Create(new EntityHandle(root)) : roots;
        var model = new RetrievalModel([new SolutionNavigation(CanonicalIdentity.CreateSolution("app", "src/App.sln"), actualRoots)], [], [], new NavigationIndexes("identity", "roots", "outgoing", "incoming", "contracts", "persistence", "evidence"));
        var plan = PackageBuilder.Build(model);
        foreach (var artifact in plan.Artifacts) package.Write(artifact.Path.Value, artifact.Payload);
        return package;
    }
    private sealed class TempPackage : IDisposable { public TempPackage() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csharp2md-journey-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); } public string Path { get; } public void Write(string relative, ImmutableArray<byte> bytes) { var file = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!); File.WriteAllBytes(file, bytes.ToArray()); } public void Dispose() => TempPath.TryDelete(Path); }
}
