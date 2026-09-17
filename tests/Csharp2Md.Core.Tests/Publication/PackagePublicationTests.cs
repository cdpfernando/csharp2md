using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.Publication;
using System.Text.Json;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class PackagePublicationTests
{
    [Fact][Trait("Requirement", "PUB-01")] public void Publish_MaterializesEveryPlannedArtifactInItsImmutableGeneration() { using var output = new TempOutput(); var plan = Plan(); PackagePublication.Publish(plan, output.Path); var generation = Path.Combine(output.Path, "generations", plan.PackageDigest); Assert.All(plan.Artifacts, artifact => Assert.True(File.Exists(Path.Combine(generation, artifact.Path.Value.Replace('/', Path.DirectorySeparatorChar))))); }
    [Fact][Trait("Requirement", "PUB-02")] public void Publish_RehydratesAndValidatesBeforeCommit() { using var output = new TempOutput(); PackagePublication.Publish(Plan(), output.Path); Assert.True(PackagePublication.Validate(output.Path).Succeeded); }
    [Fact][Trait("Requirement", "PUB-04")] public void Publish_ReturnsCommittedPackageOnlyAfterValidation() { using var output = new TempOutput(); var committed = PackagePublication.Publish(Plan(), output.Path); Assert.Equal(output.Path, committed.PackageDirectory); }
    [Fact][Trait("Requirement", "PUB-08")] public void Publish_InvalidPlanReportsStructuredPublicationCause() { using var output = new TempOutput(); var invalid = new PackagePlan(Plan().Manifest, [new PlannedArtifact(new RelativeArtifactPath("manifest.json"), ArtifactFamily.Manifest, CanonicalJson.Write(Plan().Manifest), 1, "digest")], "invalid", Plan().Measurements); Assert.StartsWith("publication:", Assert.Throws<PackagePublicationException>(() => PackagePublication.Publish(invalid, output.Path)).Message); }
    [Fact][Trait("Requirement", "PUB-05")] public void Publish_FailureBeforeCommitLeavesNoManifest() { using var output = new TempOutput(); Assert.Throws<PackagePublicationException>(() => PackagePublication.Publish(new PackagePlan(Plan().Manifest, [new PlannedArtifact(new RelativeArtifactPath("manifest.json"), ArtifactFamily.Manifest, CanonicalJson.Write(Plan().Manifest), 1, "digest")], "bad", Plan().Measurements), output.Path)); Assert.False(File.Exists(Path.Combine(output.Path, "manifest.json"))); }
    [Fact][Trait("Requirement", "PUB-01")] public void Publish_CreatesImmutableGeneration() { using var output = new TempOutput(); var plan = Plan(); PackagePublication.Publish(plan, output.Path); Assert.True(Directory.Exists(Path.Combine(output.Path, "generations", plan.PackageDigest))); }
    [Fact][Trait("Requirement", "PUB-04")] public void Publish_RootManifestExistsAfterCommit() { using var output = new TempOutput(); PackagePublication.Publish(Plan(), output.Path); Assert.True(File.Exists(Path.Combine(output.Path, "manifest.json"))); }
    [Fact][Trait("Requirement", "PUB-05")] public void Publish_StagingDirectoryIsRemovedAfterCommit() { using var output = new TempOutput(); PackagePublication.Publish(Plan(), output.Path); Assert.Empty(Directory.GetDirectories(output.Path, ".staging-*")); }
    [Fact][Trait("Requirement", "PUB-08")] public void Publish_WritesJourneyCertificationInTheGeneration() { using var output = new TempOutput(); var plan = Plan(); PackagePublication.Publish(plan, output.Path); Assert.True(File.Exists(Path.Combine(output.Path, "generations", plan.PackageDigest, "certification.json"))); }
    [Fact][Trait("Requirement", "PUB-01")] public void Publish_WritesMeasurementsArtifactInTheGeneration() { using var output = new TempOutput(); var plan = Plan(); PackagePublication.Publish(plan, output.Path); Assert.True(File.Exists(Path.Combine(output.Path, "generations", plan.PackageDigest, "measurements.json"))); }
    [Fact][Trait("Requirement", "PUB-02")] public void Validate_UsesPublishedPackageReader() { using var output = new TempOutput(); PackagePublication.Publish(Plan(), output.Path); Assert.Empty(PackagePublication.Validate(output.Path).Failures); }
    [Fact][Trait("Requirement", "PUB-05")] public void Publish_ReplacesPreviousGeneration() { using var output = new TempOutput(); PackagePublication.Publish(Plan(), output.Path); var replacement = Plan("other"); PackagePublication.Publish(replacement, output.Path); Assert.Single(Directory.GetDirectories(Path.Combine(output.Path, "generations"))); }
    [Fact][Trait("Requirement", "PUB-04")] public void Publish_RootManifestReferencesOnlyTheCommittedGeneration() { using var output = new TempOutput(); var plan = Plan(); PackagePublication.Publish(plan, output.Path); using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output.Path, "manifest.json"))); Assert.Equal(plan.PackageDigest, document.RootElement.GetProperty("generation").GetString()); Assert.DoesNotContain(Directory.GetFiles(output.Path, "*", SearchOption.TopDirectoryOnly), path => Path.GetFileName(path) is not "manifest.json" and not "package.lock"); }
    [Fact][Trait("Requirement", "EDG-03")] public void Publish_BudgetFailureOccursBeforePublication() { using var output = new TempOutput(); Assert.Throws<PackageBudgetExceededException>(() => PackageBuilder.Build(Model(), false, new PackageBudget(1, long.MaxValue))); Assert.False(File.Exists(Path.Combine(output.Path, "manifest.json"))); }
    [Fact][Trait("Requirement", "EDG-05")] public void Publish_InvalidMarkdownPlanIsRejectedBeforeManifestSwap() { using var output = new TempOutput(); var plan = Plan(); var broken = new PackagePlan(plan.Manifest, plan.Artifacts.Select(a => a.Path.Value == "markdown/index.md" ? new PlannedArtifact(a.Path, a.Family, "broken"u8.ToArray().ToImmutableArray(), a.RecordCount, a.ContentDigest) : a).ToImmutableArray(), plan.PackageDigest, plan.Measurements); Assert.Throws<PackagePublicationException>(() => PackagePublication.Publish(broken, output.Path)); Assert.False(File.Exists(Path.Combine(output.Path, "manifest.json"))); }
    [Fact][Trait("Requirement", "PUB-08")] public void Publish_CertificationFailurePreventsManifestCommit() { using var output = new TempOutput(); var failure = Assert.Throws<PackagePublicationException>(() => PackagePublication.Publish(PackageBuilder.Build(Uncertifiable()), output.Path)); Assert.Equal("publication: 'journey-certification'.", failure.Message); Assert.False(File.Exists(Path.Combine(output.Path, "manifest.json"))); Assert.False(Directory.Exists(Path.Combine(output.Path, "generations"))); }
    [Fact][Trait("Requirement", "PUB-01")] public void Publish_PreservesPlanDigest() { using var output = new TempOutput(); var plan = Plan(); Assert.Equal(plan.PackageDigest, PackagePublication.Publish(plan, output.Path).PackageDigest); }
    [Fact][Trait("Requirement", "PUB-02")] public void Publish_RecordsAllFourJourneys() { using var output = new TempOutput(); Assert.Equal(4, Assert.Single(PackagePublication.Publish(Plan(), output.Path).Certification.Solutions).Journeys.Length); }
    [Fact][Trait("Requirement", "PUB-04")] public void Publish_ValidationHasNoInterpretationDifference() { using var output = new TempOutput(); PackagePublication.Publish(Plan(), output.Path); Assert.True(PackagePublication.Validate(output.Path).Succeeded); }
    [Fact][Trait("Requirement", "PUB-05")] public void Publish_UsesExclusiveLockFile() { using var output = new TempOutput(); PackagePublication.Publish(Plan(), output.Path); Assert.True(File.Exists(Path.Combine(output.Path, "package.lock"))); }
    private static PackagePlan Plan(string solution = "app") => PackageBuilder.Build(Model(solution));
    // A solution whose only causal fact is an outgoing HTTP call has a flow root but neither a contract
    // nor a persistence terminal, so JourneyCertifier fails follow_flow and Publish must stop before the swap.
    private static RetrievalModel Uncertifiable() =>
        new([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution("app", "src/app.sln"),
            [new EntityHandle("component:orders")],
            [new AggregatedDependency(AggregationScope.Component, new EntityHandle("component:orders"), new EntityHandle("component:payments"), DependencyCategory.Http, DependencyNature.Direct, 1, [], [], [])],
            [])]);
    private static RetrievalModel Model(string solution = "app") =>
        new([new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution(solution, $"src/{solution}.sln"),
            [new EntityHandle("component:orders")],
            [],
            [])]);
    private sealed class TempOutput : IDisposable { public TempOutput() { Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csharp2md-publish-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path); } public string Path { get; } public void Dispose() => TempPath.TryDelete(Path); }
}
