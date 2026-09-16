using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class CompactDependencyReferenceTests
{
    [Fact] [Trait("Requirement", "STO-03")]
    public void Write_AssignsSortedBase36RelationHandles()
    {
        var payload = Payload(Write());
        Assert.Equal(["relation:a", "relation:z"], payload.Relations.Select(relation => relation.CanonicalKey));
        Assert.Equal(["0", "1"], payload.Dependencies.SelectMany(dependency => dependency.Relations).Select(handle => handle.Value).Order());
    }

    [Fact] [Trait("Requirement", "STO-03")]
    public void Write_AssignsSortedBase36EvidenceHandles()
    {
        var payload = Payload(Write());
        Assert.Equal(["evidence:b", "evidence:y"], payload.Evidence.ToArray());
        Assert.Equal(["0", "1"], payload.Dependencies.SelectMany(dependency => dependency.Evidence).Select(handle => handle.Value).Order());
    }

    [Fact] [Trait("Requirement", "STO-04")]
    public void Write_StoresRepeatedRelationAndEvidenceKeysOnce()
    {
        var payload = Payload(Write(repeatFirstReference: true));
        Assert.Equal(2, payload.Relations.Length);
        Assert.Equal(2, payload.Evidence.Length);
        Assert.Equal(3, payload.Dependencies.Length);
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void Write_RelationHandleResolvesToTheConfirmedFact()
    {
        var payload = Payload(Write());
        var dependency = Assert.Single(payload.Dependencies, item => item.Category == DependencyCategory.Contract);
        var relation = payload.Relations[0];
        Assert.Equal("0", Assert.Single(dependency.Relations).Value);
        Assert.Equal("relation:a", relation.CanonicalKey);
        Assert.Equal("entity:a", relation.SourceCanonicalKey);
        Assert.Equal("entity:b", relation.TargetCanonicalKey);
        Assert.Equal("contract", relation.Category);
        Assert.Equal("0", Assert.Single(relation.Evidence).Value);
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void Write_EvidenceHandleResolvesAtTheSameOrdinalInTheEvidenceIndex()
    {
        var machine = Write();
        var payload = Payload(machine);
        var path = Assert.Single(Assert.Single(machine.Manifest.Solutions).Indexes, index => index.Kind == NavigationIndexKind.Evidence).EntryPath;
        var evidence = CanonicalJson.Read<ImmutableArray<EvidenceRecord>>(Artifact(machine, path).Payload.AsSpan());
        Assert.Equal(payload.Evidence.ToArray(), evidence.Select(record => record.CanonicalKey));
        Assert.Equal("evidence:b", evidence[0].CanonicalKey);
        Assert.Equal("digest-b", evidence[0].ContentDigest);
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void Reader_RestoresCanonicalRelationAndEvidenceReferences()
    {
        var machine = Write();
        var artifacts = machine.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        var dependencies = Assert.Single(RetrievalModelReader.Read(artifacts).Solutions).Dependencies;
        Assert.Contains(dependencies, dependency => dependency.Relations.Single().Value == "relation:a" && dependency.Evidence.Single().Value == "evidence:b");
        Assert.Contains(dependencies, dependency => dependency.Relations.Single().Value == "relation:z" && dependency.Evidence.Single().Value == "evidence:y");
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void Reader_RejectsAnUnknownLocalRelationHandle()
    {
        var machine = Write();
        var path = DependencyPath(machine);
        var payload = Payload(machine);
        var edge = payload.Dependencies[0];
        var invalidEdge = new AggregatedDependency(edge.Scope, edge.Source, edge.Target, edge.Category, edge.Nature,
            edge.OccurrenceCount, edge.Variants, [new RelationHandle("zz")], edge.Evidence);
        var changed = payload with { Dependencies = payload.Dependencies.SetItem(0, invalidEdge) };
        var artifacts = machine.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        artifacts[path] = CanonicalJson.WriteCompact(changed);
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "STO-04")]
    public void Reader_RejectsDuplicateCanonicalRelationTableKeys()
    {
        var machine = Write();
        var path = DependencyPath(machine);
        var payload = Payload(machine);
        var changed = payload with { Relations = payload.Relations.SetItem(1, payload.Relations[1] with { CanonicalKey = payload.Relations[0].CanonicalKey }) };
        var artifacts = machine.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        artifacts[path] = CanonicalJson.WriteCompact(changed);
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "DEP-07")]
    public void Reader_RejectsAnEvidenceIndexThatNoLongerMatchesLocalHandles()
    {
        var machine = Write();
        var path = Assert.Single(Assert.Single(machine.Manifest.Solutions).Indexes, index => index.Kind == NavigationIndexKind.Evidence).EntryPath;
        var evidence = CanonicalJson.Read<ImmutableArray<EvidenceRecord>>(Artifact(machine, path).Payload.AsSpan());
        var artifacts = machine.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);
        artifacts[path] = CanonicalJson.Write(evidence.Reverse().ToImmutableArray());
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "EDG-01")]
    public void Write_RejectsAReferenceWithNoRetainedConfirmedRelation()
    {
        Assert.Throws<InvalidOperationException>(() => Write(omitSecondFact: true));
    }

    [Fact] [Trait("Requirement", "EDG-01")]
    public void Write_RejectsAReferenceWithNoRetainedEvidence()
    {
        Assert.Throws<InvalidOperationException>(() => Write(omitSecondEvidence: true));
    }

    [Fact] [Trait("Requirement", "STO-07")]
    public void Write_SameModelProducesIdenticalDependencyBytes()
    {
        var first = Write();
        var second = Write();
        Assert.True(Artifact(first, DependencyPath(first)).Payload.AsSpan().SequenceEqual(Artifact(second, DependencyPath(second)).Payload.AsSpan()));
    }

    private static DependencyPayload Payload(MachineArtifactSet machine) =>
        CanonicalJson.Read<DependencyPayload>(Artifact(machine, DependencyPath(machine)).Payload.AsSpan());

    private static string DependencyPath(MachineArtifactSet machine) =>
        Assert.Single(machine.Artifacts, artifact => artifact.Path.Value.EndsWith("/measures/dependencies.000000.json", StringComparison.Ordinal)).Path.Value;

    private static PlannedArtifact Artifact(MachineArtifactSet machine, string path) =>
        Assert.Single(machine.Artifacts, artifact => artifact.Path.Value == path);

    private static MachineArtifactSet Write(bool repeatFirstReference = false, bool omitSecondFact = false, bool omitSecondEvidence = false) =>
        MachineArtifactWriter.Write(Model(repeatFirstReference, omitSecondFact, omitSecondEvidence), includeTests: false);

    private static RetrievalModel Model(bool repeatFirstReference, bool omitSecondFact, bool omitSecondEvidence)
    {
        var solution = CanonicalIdentity.CreateSolution("app", "src/App.sln");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var span = new SourceSpan(1, 1, 1, 1);
        var relations = omitSecondFact
            ? ImmutableArray.Create(new FactualRelation("relation:a", "entity:a", "entity:b", "contract", ["evidence:b"]))
            : ImmutableArray.Create(
                new FactualRelation("relation:a", "entity:a", "entity:b", "contract", ["evidence:b"]),
                new FactualRelation("relation:z", "entity:b", "entity:c", "persistence", ["evidence:y"]));
        var firstEvidence = new EvidenceRecord("evidence:b", CanonicalIdentity.CreateDocumentKey(solution, "A.cs"), variant, span, "digest-b");
        var evidence = omitSecondEvidence
            ? ImmutableArray.Create(firstEvidence)
            : ImmutableArray.Create(firstEvidence,
                new EvidenceRecord("evidence:y", CanonicalIdentity.CreateDocumentKey(solution, "B.cs"), variant, span, "digest-y"));
        var facts = new RetainedGraph(
            [],
            relations,
            [],
            evidence,
            [],
            new RetentionMeasurements(2, 0));
        var first = Dependency("relation:a", "evidence:b", DependencyCategory.Contract);
        var second = Dependency("relation:z", "evidence:y", DependencyCategory.Persistence);
        var dependencies = repeatFirstReference
            ? ImmutableArray.Create(first, second, Dependency("relation:a", "evidence:b", DependencyCategory.Http))
            : ImmutableArray.Create(first, second);
        return new RetrievalModel([new SolutionRetrievalModel(solution, [new EntityHandle("entity:a")], dependencies, [], facts)]);
    }

    private static AggregatedDependency Dependency(string relation, string evidence, DependencyCategory category) =>
        new(AggregationScope.Component, new EntityHandle("entity:a"), new EntityHandle("entity:b"), category, DependencyNature.Direct, 1, [], [new RelationHandle(relation)], [new EvidenceHandle(evidence)]);
}
