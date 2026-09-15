using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class PackageBuilderTests
{
    [Fact][Trait("Requirement", "PKG-01")] public void Build_ContainsRootManifest() => Assert.Contains(Plan().Artifacts, artifact => artifact.Path.Value == "manifest.json");
    [Fact][Trait("Requirement", "PKG-03")] public void Build_ContainsAllMachineAndMarkdownBytes() => Assert.All(Plan().Artifacts, artifact => Assert.False(artifact.Payload.IsDefaultOrEmpty));
    [Fact][Trait("Requirement", "PKG-04")] public void Build_ReservesMeasurementsBeforeStaging() => Assert.Contains(Plan().Artifacts, artifact => artifact.Path.Value == "measurements.json");
    [Fact][Trait("Requirement", "PKG-05")] public void Build_ReservesCertificationBeforeStaging() => Assert.Contains(Plan().Artifacts, artifact => artifact.Path.Value == "certification.json");
    [Fact][Trait("Requirement", "PKG-06")] public void Build_PreservesMarkdownArtifacts() => Assert.Contains(Plan().Artifacts, artifact => artifact.Family == ArtifactFamily.Markdown);
    [Fact][Trait("Requirement", "PKG-08")] public void Build_RecordsIncludeTestsInManifest() => Assert.True(PackageBuilder.Build(Model(), true).Manifest.IncludeTests);
    [Fact][Trait("Requirement", "STO-06")] public void Build_OrdersArtifactsCanonically() => Assert.Equal(Plan().Artifacts.Select(artifact => artifact.Path.Value).Order(StringComparer.Ordinal), Plan().Artifacts.Select(artifact => artifact.Path.Value));
    [Fact][Trait("Requirement", "STO-07")] public void Build_IsPermutationStable() { var first=PackageBuilder.Build(Model()); var second=PackageBuilder.Build(Model()); Assert.Equal(first.PackageDigest, second.PackageDigest); Assert.Equal(first.Artifacts.Select(artifact => artifact.Payload), second.Artifacts.Select(artifact => artifact.Payload)); }
    [Fact][Trait("Requirement", "CRT-03")] public void Build_SeparatesExtractionAndPublicationMeasurements() { var measurements=Plan().Measurements; Assert.Equal(0, measurements.Extraction.ExtractedCount); Assert.Equal(Plan().Artifacts.Length, measurements.PublishedArtifactCount); }
    [Fact][Trait("Requirement", "CRT-03")] public void Build_RecordsFilteredReason() => Assert.Equal("retention", Assert.Single(Plan().Measurements.FilteredByReason).Reason);
    [Fact][Trait("Requirement", "EDG-03")] public void Build_FailsBeforePublicationWhenArtifactCeilingIsExceeded() => Assert.Equal("package-budget: 'artifacts'.", Assert.Throws<PackageBudgetExceededException>(() => PackageBuilder.Build(Model(), false, new PackageBudget(1, long.MaxValue))).Message);
    [Fact][Trait("Requirement", "EDG-03")] public void Build_FailsBeforePublicationWhenByteCeilingIsExceeded() => Assert.Equal("package-budget: 'bytes'.", Assert.Throws<PackageBudgetExceededException>(() => PackageBuilder.Build(Model(), false, new PackageBudget(int.MaxValue, 1))).Message);
    [Fact][Trait("Requirement", "PKG-01")] public void Build_AcceptsMultipleSolutions() { var model=Model(); var extra=new SolutionRetrievalModel(CanonicalIdentity.CreateSolution("other","src/Other.sln"),[new EntityHandle("component:other")], [], []); var plan=PackageBuilder.Build(new RetrievalModel(model.Solutions.Add(extra))); Assert.Equal(2, plan.Manifest.Solutions.Length); }
    [Fact][Trait("Requirement", "STO-04")] public void Build_AssignsOneArtifactPathPerPayload() => Assert.Equal(Plan().Artifacts.Length, Plan().Artifacts.Select(artifact => artifact.Path.Value).Distinct(StringComparer.Ordinal).Count());
    private static PackagePlan Plan() => PackageBuilder.Build(Model());
    private static RetrievalModel Model() =>
        new([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution("app", "src/App.sln"),
            [new EntityHandle("component:orders")],
            [new AggregatedDependency(AggregationScope.Component, new EntityHandle("component:orders"), new EntityHandle("component:billing"), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], [])],
            [new ScopeMeasures(AggregationScope.Component, new EntityHandle("component:orders"), 0, 1, 0, [], [], new GapCounts(0, 0, 0))])]);
}
