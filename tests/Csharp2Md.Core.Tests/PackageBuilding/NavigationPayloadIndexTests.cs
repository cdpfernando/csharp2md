using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class NavigationPayloadIndexTests
{
    [Fact] [Trait("Requirement", "DEP-05")]
    public void Write_StoresTheDependencyArrayAtOnePath()
    {
        var package = Write();
        Assert.Single(package.Artifacts, artifact => artifact.Path.Value.EndsWith("/measures/dependencies.000000.json", StringComparison.Ordinal));
        Assert.DoesNotContain(package.Artifacts, artifact => artifact.Path.Value.EndsWith("/graph/relations.000000.json", StringComparison.Ordinal));
        Assert.All(
            Enum.GetValues<NavigationIndexKind>().Where(kind => kind is NavigationIndexKind.Outgoing or NavigationIndexKind.Incoming or NavigationIndexKind.Contracts or NavigationIndexKind.Persistence),
            kind => Assert.Equal(DependenciesPath(package), Index(package, kind).ArtifactPath));
    }

    [Fact] [Trait("Requirement", "DEP-05")]
    public void Write_StoresTheMeasureArrayAtOnePath()
    {
        var package = Write();
        Assert.Single(package.Artifacts, artifact => artifact.Path.Value.EndsWith("/measures/summary.json", StringComparison.Ordinal));
        Assert.DoesNotContain(package.Artifacts, artifact => artifact.Path.Value.EndsWith("/graph/gaps.000000.json", StringComparison.Ordinal));
        Assert.Equal(MeasuresPath(package), Index(package, NavigationIndexKind.Measures).ArtifactPath);
    }

    [Fact] [Trait("Requirement", "NAV-01")]
    public void Write_DeclaresEachIndexKindOnce() =>
        Assert.Equal(
            Enum.GetValues<NavigationIndexKind>().Order(),
            Assert.Single(Write().Manifest.Solutions).Indexes.Select(index => index.Kind).Order());

    [Fact] [Trait("Requirement", "DEP-07")]
    public void OutgoingIndex_ResolvesEachSourceToItsDependencyRecords()
    {
        var package = Write();
        var dependencies = Dependencies(package);
        var index = Index(package, NavigationIndexKind.Outgoing);
        Assert.Equal(dependencies.Length, index.Entries.Sum(entry => entry.Ordinals.Length));
        Assert.All(index.Entries, entry => Assert.All(entry.Ordinals, ordinal =>
            Assert.Equal(entry.Key, dependencies[ordinal].Source.Value)));
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void IncomingIndex_ResolvesEachTargetToItsDependencyRecords()
    {
        var package = Write();
        var dependencies = Dependencies(package);
        var index = Index(package, NavigationIndexKind.Incoming);
        Assert.Equal(dependencies.Length, index.Entries.Sum(entry => entry.Ordinals.Length));
        Assert.All(index.Entries, entry => Assert.All(entry.Ordinals, ordinal =>
            Assert.Equal(entry.Key, dependencies[ordinal].Target.Value)));
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void ContractsIndex_ResolvesOnlyContracts() => CategoryIndexResolvesOnly(NavigationIndexKind.Contracts, DependencyCategory.Contract);

    [Fact] [Trait("Requirement", "DEP-07")]
    public void PersistenceIndex_ResolvesOnlyPersistence() => CategoryIndexResolvesOnly(NavigationIndexKind.Persistence, DependencyCategory.Persistence);

    private static void CategoryIndexResolvesOnly(NavigationIndexKind kind, DependencyCategory category)
    {
        var package = Write();
        var dependencies = Dependencies(package);
        var index = Index(package, kind);
        Assert.NotEmpty(index.Entries);
        Assert.All(index.Entries, entry => Assert.All(entry.Ordinals, ordinal =>
            Assert.Equal(category, dependencies[ordinal].Category)));
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void MeasuresIndex_ResolvesEachEntityAndReachableSet()
    {
        var package = Write();
        var measures = CanonicalJson.Read<ImmutableArray<ScopeMeasures>>(Artifact(package, MeasuresPath(package)).Payload.AsSpan());
        var index = Index(package, NavigationIndexKind.Measures);
        Assert.Equal(measures.Length, index.Entries.Sum(entry => entry.Ordinals.Length));
        Assert.All(index.Entries, entry => Assert.All(entry.Ordinals, ordinal =>
            Assert.Equal(entry.Key, measures[ordinal].Entity.Value)));
        Assert.Contains(index.Entries, entry => entry.HasReachableSet);
    }

    [Fact] [Trait("Requirement", "NAV-05")]
    public void Reader_RehydratesFromTheSinglePayloadArtifacts()
    {
        var package = Write();
        var artifacts = package.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        var solution = Assert.Single(RetrievalModelReader.Read(artifacts).Solutions);
        Assert.Equal(3, solution.Dependencies.Length);
        Assert.Equal(2, solution.Measures.Length);
        var contract = Assert.Single(solution.Dependencies, dependency => dependency.Category == DependencyCategory.Contract);
        Assert.Equal("relation:Contract", Assert.Single(contract.Relations).Value);
        Assert.Equal("evidence:Contract", Assert.Single(contract.Evidence).Value);
        Assert.Contains(solution.Measures, measure => !measure.ReverseImpact.IsDefaultOrEmpty);
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void PackageReader_FollowsDeclaredIndexPointersToBothPayloads()
    {
        var directory = Path.Combine(Path.GetTempPath(), "csharp2md-index-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var plan = PackageBuilder.Build(Model());
            foreach (var artifact in plan.Artifacts)
            {
                var path = Path.Combine(directory, artifact.Path.Value.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, artifact.Payload.ToArray());
            }

            using var reader = PackageReader.Open(directory);
            var loaded = reader.ReadDeclaredArtifacts();
            var package = Write();
            Assert.Contains(DependenciesPath(package), loaded.Keys);
            Assert.Contains(MeasuresPath(package), loaded.Keys);
        }
        finally
        {
            TempPath.TryDelete(directory);
        }
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void Reader_RejectsAnUnresolvableDependencyOrdinal()
    {
        var package = Write();
        var path = Assert.Single(Assert.Single(package.Manifest.Solutions).Indexes, index => index.Kind == NavigationIndexKind.Outgoing).EntryPath;
        var index = Index(package, NavigationIndexKind.Outgoing);
        var entries = index.Entries.SetItem(0, index.Entries[0] with { Ordinals = [999] });
        var artifacts = package.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        artifacts[path] = CanonicalJson.Write(index with { Entries = entries });
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void Reader_RejectsAnIndexPointingAtTheWrongArtifact()
    {
        var package = Write();
        var path = Assert.Single(Assert.Single(package.Manifest.Solutions).Indexes, index => index.Kind == NavigationIndexKind.Outgoing).EntryPath;
        var index = Index(package, NavigationIndexKind.Outgoing);
        var artifacts = package.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        artifacts[path] = CanonicalJson.Write(index with { ArtifactPath = MeasuresPath(package) });
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    private static MachineArtifactSet Write() => MachineArtifactWriter.Write(Model(), includeTests: false);

    private static NavigationIndexData Index(MachineArtifactSet package, NavigationIndexKind kind)
    {
        var path = Assert.Single(Assert.Single(package.Manifest.Solutions).Indexes, index => index.Kind == kind).EntryPath;
        return CanonicalJson.Read<NavigationIndexData>(Artifact(package, path).Payload.AsSpan());
    }

    private static ImmutableArray<AggregatedDependency> Dependencies(MachineArtifactSet package) =>
        CanonicalJson.Read<DependencyPayload>(Artifact(package, DependenciesPath(package)).Payload.AsSpan()).Dependencies;

    private static string DependenciesPath(MachineArtifactSet package) =>
        Assert.Single(package.Artifacts, artifact => artifact.Path.Value.EndsWith("/measures/dependencies.000000.json", StringComparison.Ordinal)).Path.Value;

    private static string MeasuresPath(MachineArtifactSet package) =>
        Assert.Single(package.Artifacts, artifact => artifact.Path.Value.EndsWith("/measures/summary.json", StringComparison.Ordinal)).Path.Value;

    private static PlannedArtifact Artifact(MachineArtifactSet package, string path) =>
        Assert.Single(package.Artifacts, artifact => artifact.Path.Value == path);

    private static RetrievalModel Model()
    {
        var a = new EntityHandle("component:a");
        var b = new EntityHandle("component:b");
        var c = new EntityHandle("component:c");
        var solution = CanonicalIdentity.CreateSolution("app", "src/App.sln");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var span = new SourceSpan(1, 1, 1, 1);
        var facts = new RetainedGraph(
            [],
            [
                new FactualRelation("relation:Contract", "component:a", "component:b", "contract", ["evidence:Contract"]),
                new FactualRelation("relation:Persistence", "component:a", "component:c", "persistence", ["evidence:Persistence"]),
                new FactualRelation("relation:Http", "component:c", "component:b", "http", ["evidence:Http"]),
            ],
            [],
            [
                new EvidenceRecord("evidence:Contract", CanonicalIdentity.CreateDocumentKey(solution, "A.cs"), variant, span, "contract-digest"),
                new EvidenceRecord("evidence:Persistence", CanonicalIdentity.CreateDocumentKey(solution, "B.cs"), variant, span, "persistence-digest"),
                new EvidenceRecord("evidence:Http", CanonicalIdentity.CreateDocumentKey(solution, "C.cs"), variant, span, "http-digest"),
            ],
            [],
            new RetentionMeasurements(3, 0));
        return new RetrievalModel([new SolutionRetrievalModel(
            solution,
            [a],
            [Dependency(a, b, DependencyCategory.Contract), Dependency(a, c, DependencyCategory.Persistence), Dependency(c, b, DependencyCategory.Http)],
            [
                new ScopeMeasures(AggregationScope.Component, a, 0, 2, 0, [], [new ImpactTarget(b, 1)], new GapCounts(0, 0, 0)),
                new ScopeMeasures(AggregationScope.Component, b, 2, 0, 0, [], [], new GapCounts(0, 0, 0)),
            ],
            facts)]);
    }

    private static AggregatedDependency Dependency(EntityHandle source, EntityHandle target, DependencyCategory category) =>
        new(AggregationScope.Component, source, target, category, DependencyNature.Direct, 1, [], [new RelationHandle($"relation:{category}")], [new EvidenceHandle($"evidence:{category}")]);
}
