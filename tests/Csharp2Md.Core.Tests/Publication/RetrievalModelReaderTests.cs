using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class RetrievalModelReaderTests
{
    [Fact][Trait("Requirement", "NAV-05")] public void Read_RoundTripsSolution() => Assert.Equal("src/App.sln", Assert.Single(RoundTrip().Solutions).Solution.LogicalRelativePath);
    [Fact][Trait("Requirement", "NAV-05")] public void Read_RoundTripsRoots() => Assert.Equal("component:orders", Assert.Single(Assert.Single(RoundTrip().Solutions).Roots).Value);
    [Fact][Trait("Requirement", "NAV-05")] public void Read_RoundTripsDependencies() => Assert.Equal("component:billing", Assert.Single(Assert.Single(RoundTrip().Solutions).Dependencies).Target.Value);
    [Fact][Trait("Requirement", "NAV-05")] public void Read_RoundTripsMeasures() => Assert.Equal(1, Assert.Single(Assert.Single(RoundTrip().Solutions).Measures).FanOut);
    [Fact][Trait("Requirement", "NAV-05")] public void Read_RoundTripsSolutionScopedIndexes() { var solution = Assert.Single(Manifest().Solutions); Assert.Equal(8, solution.Indexes.Length); Assert.Contains(solution.Indexes, index => index.Kind == NavigationIndexKind.Evidence); }
    [Fact][Trait("Requirement", "PUB-03")] public void Read_MissingDeclaredArtifactReportsArtifact() { var artifacts=Artifacts(); var path=Assert.Single(Manifest().Solutions).Indexes.Single(index => index.Kind == NavigationIndexKind.Outgoing).EntryPath; artifacts.Remove(path); Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact); }
    [Fact][Trait("Requirement", "PUB-03")] public void Read_MissingManifestReportsManifest() { var artifacts=Artifacts(); artifacts.Remove("manifest.json"); Assert.Equal("manifest.json", Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact); }
    [Fact][Trait("Requirement", "NAV-05")] public void VerifyMarkdown_AcceptsEquivalentBytes() => RetrievalModelReader.VerifyMarkdown(Artifacts());
    [Fact][Trait("Requirement", "EDG-05")] public void VerifyMarkdown_RejectsMutatedMarkdownWithPath() { var artifacts=Artifacts(); var path=Assert.Single(Manifest().Solutions).Roots[0].MarkdownPath; artifacts[path]=Encoding.UTF8.GetBytes("changed\n").ToImmutableArray(); Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.VerifyMarkdown(artifacts)).Artifact); }
    [Fact][Trait("Requirement", "EDG-05")] public void VerifyMarkdown_RejectsMissingMarkdownWithPath() { var artifacts=Artifacts(); var path=Assert.Single(Manifest().Solutions).Roots[0].MarkdownPath; artifacts.Remove(path); Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.VerifyMarkdown(artifacts)).Artifact); }
    private static RetrievalModel RoundTrip() => RetrievalModelReader.Read(Artifacts());
    private static PackageManifest Manifest() => Machine().Manifest;
    private static Dictionary<string, ImmutableArray<byte>> Artifacts() { var machine=Machine(); return machine.Artifacts.AddRange(MarkdownRenderer.Render(Model(), machine.Manifest)).ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal); }
    private static MachineArtifactSet Machine() => MachineArtifactWriter.Write(Model(), false);
    private static RetrievalModel Model() =>
        new([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution("app", "src/App.sln"),
            [new EntityHandle("component:orders")],
            [new AggregatedDependency(AggregationScope.Component, new EntityHandle("component:orders"), new EntityHandle("component:billing"), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], [])],
            [new ScopeMeasures(AggregationScope.Component, new EntityHandle("component:orders"), 0, 1, 0, [], [], new GapCounts(0, 0, 0))])]);
}
