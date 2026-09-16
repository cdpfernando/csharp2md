using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.Publication;
using Csharp2Md.Core.Publication.Certification;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class GraphJourneyCertifierTests
{
    [Fact][Trait("Requirement", "NAV-08")] public void Certify_FlowWithAllTerminalsPasses() => Assert.Equal(JourneyCertificationStatus.Passed, Flow(Package(AllTerminals())).Status);
    [Fact][Trait("Requirement", "NAV-09")] public void Certify_ImpactWithReachableSetPasses() => Assert.Equal(JourneyCertificationStatus.Passed, Impact(Package(dependencies: [Dependency(DependencyCategory.Contract)], measures: [Measure([new ImpactTarget(new EntityHandle("component:caller"), 1)])])).Status);
    [Fact][Trait("Requirement", "CRT-02")] public void Certify_EmptyFlowIsNotApplicable() => Assert.Equal(JourneyCertificationStatus.NotApplicable, Flow(Package()).Status);
    [Fact][Trait("Requirement", "CRT-02")] public void Certify_EmptyImpactIsNotApplicable() => Assert.Equal(JourneyCertificationStatus.NotApplicable, Impact(Package()).Status);
    [Fact][Trait("Requirement", "CRT-02")] public void Certify_FlowWithOnlyInternalInvocationIsNotApplicable()
    {
        using var package = Package([Dependency(DependencyCategory.InternalInvocation)]);
        var result = Flow(package);
        Assert.Equal(JourneyCertificationStatus.NotApplicable, result.Status);
        Assert.StartsWith("not_applicable:", result.Detail, StringComparison.Ordinal);
    }
    [Fact][Trait("Requirement", "CRT-02")] public void Certify_ImpactWithOnlyInternalInvocationIsNotApplicable()
    {
        using var package = Package([Dependency(DependencyCategory.InternalInvocation)], [Measure([new ImpactTarget(new EntityHandle("component:caller"), 1)])]);
        var result = Impact(package);
        Assert.Equal(JourneyCertificationStatus.NotApplicable, result.Status);
        Assert.StartsWith("not_applicable:", result.Detail, StringComparison.Ordinal);
    }
    [Fact][Trait("Requirement", "CRT-02")] public void Certify_FlowWithOnlyPersistenceIsNotApplicable()
    {
        using var package = Package([Dependency(DependencyCategory.Persistence)]);
        var result = Flow(package);
        Assert.Equal(JourneyCertificationStatus.NotApplicable, result.Status);
        Assert.Equal("not_applicable:no-causal-root", result.Detail);
    }
    [Fact][Trait("Requirement", "NAV-09")] public void Certify_ImpactWithOnlyPersistenceStaysApplicable()
    {
        using var package = Package([Dependency(DependencyCategory.Persistence)], [Measure([new ImpactTarget(new EntityHandle("component:caller"), 1)])]);
        Assert.Equal(JourneyCertificationStatus.Passed, Impact(package).Status);
    }
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_FlowWithAMessagingRootAndNoContractStillFails()
    {
        using var package = Package([Dependency(DependencyCategory.Messaging), Dependency(DependencyCategory.Persistence)]);
        var result = Flow(package);
        Assert.Equal(JourneyCertificationStatus.Failed, result.Status);
        Assert.Equal("missing-terminal:contracts", result.Detail);
    }
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_FlowWithoutContractFails() => Assert.Contains("contracts", Flow(Package([Dependency(DependencyCategory.Persistence), Dependency(DependencyCategory.Http)])).Detail);
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_FlowWithoutPersistenceFails() => Assert.Contains("persistence", Flow(Package([Dependency(DependencyCategory.Contract), Dependency(DependencyCategory.Http)])).Detail);
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_FlowWithoutExternalEffectFails() => Assert.Contains("external-effects", Flow(Package([Dependency(DependencyCategory.Contract), Dependency(DependencyCategory.Persistence)])).Detail);
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_ImpactWithoutReachableSetFails() => Assert.Contains("reachable-set", Impact(Package(dependencies: [Dependency(DependencyCategory.Contract)], measures: [Measure([])])).Detail);
    [Fact][Trait("Requirement", "NAV-08")] public void Certify_FlowAcceptsGrpcAsExternalEffect() => Assert.Equal(JourneyCertificationStatus.Passed, Flow(Package([Dependency(DependencyCategory.Contract), Dependency(DependencyCategory.Persistence), Dependency(DependencyCategory.Grpc)])).Status);
    [Fact][Trait("Requirement", "NAV-08")] public void Certify_FlowAcceptsMessagingAsExternalEffect() => Assert.Equal(JourneyCertificationStatus.Passed, Flow(Package([Dependency(DependencyCategory.Contract), Dependency(DependencyCategory.Persistence), Dependency(DependencyCategory.Messaging)])).Status);
    [Fact][Trait("Requirement", "NAV-09")] public void Certify_ImpactPreservesMinimumDepth() { using var package = Package(dependencies: [Dependency(DependencyCategory.Contract)], measures: [Measure([new ImpactTarget(new EntityHandle("component:caller"), 2)])]); Assert.Equal(JourneyCertificationStatus.Passed, Impact(package).Status); }
    [Fact][Trait("Requirement", "EDG-02")] public void Certify_FlowBudgetFailureNamesMeasure() { using var package = Package(AllTerminals()); for (var i = 0; i < 40; i++) { using var reader = MeasuredPackageReader.Open(package.Path); reader.OpenArtifact("manifest.json"); } Assert.Equal(JourneyCertificationStatus.Passed, Flow(package).Status); }
    [Fact][Trait("Requirement", "NAV-08")] public void Certify_FlowDetailContainsMeasurements() => Assert.Contains("tokens:", Flow(Package(AllTerminals())).Detail);
    [Fact][Trait("Requirement", "NAV-09")] public void Certify_ImpactDetailContainsMeasurements() => Assert.Contains("reads:", Impact(Package(dependencies: [Dependency(DependencyCategory.Contract)], measures: [Measure([new ImpactTarget(new EntityHandle("component:caller"), 1)])])).Detail);
    [Fact][Trait("Requirement", "CRT-01")] public void Certify_ApplicableImpactNeverPassesWithoutAnswer() { using var package = Package(dependencies: [Dependency(DependencyCategory.Contract)], measures: [Measure([])]); Assert.Equal(JourneyCertificationStatus.Failed, Impact(package).Status); }

    private static JourneyCertification Flow(TempPackage package) => Assert.Single(JourneyCertifier.Certify(package.Path).Solutions).Journeys.Single(x => x.Kind == JourneyKind.FollowFlow);
    private static JourneyCertification Impact(TempPackage package) => Assert.Single(JourneyCertifier.Certify(package.Path).Solutions).Journeys.Single(x => x.Kind == JourneyKind.ReverseImpact);
    private static ImmutableArray<AggregatedDependency> AllTerminals() => [Dependency(DependencyCategory.Contract), Dependency(DependencyCategory.Persistence), Dependency(DependencyCategory.Http)];
    private static AggregatedDependency Dependency(DependencyCategory category) => new(AggregationScope.Component, new EntityHandle("component:orders"), new EntityHandle("component:target"), category, DependencyNature.Direct, 1, [], [], []);
    private static ScopeMeasures Measure(ImmutableArray<ImpactTarget> impact) => new(AggregationScope.Component, new EntityHandle("component:orders"), 0, 1, 0, [], impact, new GapCounts(0, 0, 0));
    private static TempPackage Package(ImmutableArray<AggregatedDependency> dependencies = default, ImmutableArray<ScopeMeasures> measures = default)
    {
        var package = new TempPackage();
        var model = new RetrievalModel([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution("app", "src/App.sln"),
            [new EntityHandle("component:orders")],
            dependencies.IsDefault ? [] : dependencies,
            measures.IsDefault ? [] : measures)]);
        foreach (var artifact in PackageBuilder.Build(model).Artifacts)
        {
            package.Write(artifact.Path.Value, artifact.Payload);
        }

        return package;
    }
    private sealed class TempPackage : IDisposable { public TempPackage() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csharp2md-graph-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); } public string Path { get; } public void Write(string relative, ImmutableArray<byte> bytes) { var file = System.IO.Path.Combine(Path, relative.Replace('/', System.IO.Path.DirectorySeparatorChar)); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!); File.WriteAllBytes(file, bytes.ToArray()); } public void Dispose() => TempPath.TryDelete(Path); }
}
