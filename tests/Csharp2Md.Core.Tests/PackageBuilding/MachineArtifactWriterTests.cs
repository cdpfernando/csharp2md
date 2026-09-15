using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class MachineArtifactWriterTests
{
    [Fact][Trait("Requirement", "PKG-01")] public void Write_ContainsOneRootManifest() => Assert.Single(Write().Artifacts.Where(artifact => artifact.Path.Value == "manifest.json"));
    [Fact][Trait("Requirement", "PKG-01")] public void Write_ListsSolutionLogicalPath() => Assert.Equal("src/App.sln", Assert.Single(Write().Manifest.Solutions).LogicalRelativePath);
    [Fact][Trait("Requirement", "PKG-01")] public void Write_ListsProvenRoot() => Assert.Equal("component:orders", Assert.Single(Write().Manifest.Roots).DisplayName);
    [Fact][Trait("Requirement", "STO-05")] public void Write_RootCitesMachineArtifactByHandle() => Assert.Matches("graph/entities.000000.json#[0-9a-z]{1,6}$", Assert.Single(Write().Manifest.Roots).MachineCitation);
    [Fact][Trait("Requirement", "NAV-01")] public void Write_DeclaresIdentityIndex() => Assert.Contains(Write().Manifest.Indexes, index => index.Name == "identity");
    [Fact][Trait("Requirement", "NAV-01")] public void Write_DeclaresRootsIndex() => Assert.Contains(Write().Manifest.Indexes, index => index.Name == "roots");
    [Fact][Trait("Requirement", "NAV-01")] public void Write_DeclaresOutgoingIndex() => Assert.Contains(Write().Manifest.Indexes, index => index.Name == "outgoing");
    [Fact][Trait("Requirement", "NAV-01")] public void Write_DeclaresIncomingIndex() => Assert.Contains(Write().Manifest.Indexes, index => index.Name == "incoming");
    [Fact][Trait("Requirement", "NAV-01")] public void Write_DeclaresContractsIndex() => Assert.Contains(Write().Manifest.Indexes, index => index.Name == "contracts");
    [Fact][Trait("Requirement", "NAV-01")] public void Write_DeclaresPersistenceIndex() => Assert.Contains(Write().Manifest.Indexes, index => index.Name == "persistence");
    [Fact][Trait("Requirement", "NAV-01")] public void Write_DeclaresEvidenceIndex() => Assert.Contains(Write().Manifest.Indexes, index => index.Name == "evidence");
    [Fact][Trait("Requirement", "NAV-05")] public void Write_DeclaresFourJourneyEntries() => Assert.Equal(4, Write().Manifest.Journeys.Length);
    [Fact][Trait("Requirement", "STO-05")] public void Write_UsesOnlyRelativeNormalizedArtifactPaths() => Assert.All(Write().Artifacts, artifact => Assert.Equal(artifact.Path.Value, new RelativeArtifactPath(artifact.Path.Value).Value));
    [Fact][Trait("Requirement", "STO-07")] public void Write_IsByteStableForSameModel() { var first=Write(); var second=Write(); Assert.Equal(first.Artifacts.Select(artifact => artifact.Payload), second.Artifacts.Select(artifact => artifact.Payload)); }
    private static MachineArtifactSet Write() => MachineArtifactWriter.Write(Model(), includeTests: false);
    private static RetrievalModel Model() { var solution=CanonicalIdentity.CreateSolution("app", "src/App.sln"); return new RetrievalModel([new SolutionNavigation(solution, [new EntityHandle("component:orders")])], [], [], new NavigationIndexes("identity", "roots", "outgoing", "incoming", "contracts", "persistence", "evidence")); }
}
