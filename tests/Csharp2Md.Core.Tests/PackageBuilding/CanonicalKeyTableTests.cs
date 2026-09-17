using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class CanonicalKeyTableTests
{
    [Fact] [Trait("Requirement", "STO-04")]
    public void Write_StoresEachEntityKeyOnceInTheDeclaredTable()
    {
        var machine = Write();
        Assert.Equal(["entity:a", "entity:b", "entity:c"], Table(machine, "entities").AsEnumerable());
    }

    [Fact] [Trait("Requirement", "STO-04")]
    public void Write_StoresEachVariantAndCycleKeyOnceInItsDeclaredTable()
    {
        var machine = Write();
        Assert.Equal(["variant:v"], Table(machine, "variants").AsEnumerable());
        Assert.Equal(["cycle:a"], Table(machine, "cycles").AsEnumerable());
    }

    [Fact] [Trait("Requirement", "STO-03")]
    public void Write_ReplacesRelationEndpointsWithEntityHandles()
    {
        var relation = Assert.Single(Payload(Write()).Relations);
        Assert.Equal("0", relation.SourceCanonicalKey);
        Assert.Equal("1", relation.TargetCanonicalKey);
    }

    [Fact] [Trait("Requirement", "STO-03")]
    public void Write_ReplacesDependencyEndpointsAndVariantsWithHandles()
    {
        var dependency = Assert.Single(Payload(Write()).Dependencies);
        Assert.Equal("0", dependency.Source.Value);
        Assert.Equal("1", dependency.Target.Value);
        Assert.Equal(["0"], dependency.Variants.Select(variant => variant.Value));
    }

    [Fact] [Trait("Requirement", "STO-03")]
    public void Write_ReplacesMeasureEntityCycleAndReverseImpactWithHandles()
    {
        var measure = Assert.Single(Measures(Write()));
        Assert.Equal("0", measure.Entity.Value);
        Assert.Equal(["0"], measure.Cycles.Select(cycle => cycle.Value));
        Assert.Equal("2", Assert.Single(measure.ReverseImpact).Entity.Value);
    }

    [Fact] [Trait("Requirement", "DEP-03")]
    public void Read_RestoresCanonicalEntityVariantAndCycleValues()
    {
        var solution = Assert.Single(RetrievalModelReader.Read(Artifacts(Write())).Solutions);
        var dependency = Assert.Single(solution.Dependencies);
        Assert.Equal("entity:a", dependency.Source.Value);
        Assert.Equal("entity:b", dependency.Target.Value);
        Assert.Equal(["variant:v"], dependency.Variants.Select(variant => variant.Value));
        var measure = Assert.Single(solution.Measures);
        Assert.Equal("entity:a", measure.Entity.Value);
        Assert.Equal(["cycle:a"], measure.Cycles.Select(cycle => cycle.Value));
        Assert.Equal("entity:c", Assert.Single(measure.ReverseImpact).Entity.Value);
    }

    [Fact] [Trait("Requirement", "STO-05")]
    public void Read_RejectsADependencyEntityHandleWithNoTableRow()
    {
        var machine = Write();
        var path = DependencyPath(machine);
        var payload = Payload(machine);
        var edge = payload.Dependencies[0];
        var artifacts = Artifacts(machine);
        artifacts[path] = CanonicalJson.WriteCompact(payload with
        {
            Dependencies = payload.Dependencies.SetItem(0, new AggregatedDependency(
                edge.Scope, new EntityHandle("zz"), edge.Target, edge.Category, edge.Nature,
                edge.OccurrenceCount, edge.Variants, edge.Relations, edge.Evidence)),
        });
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "STO-05")]
    public void Read_RejectsAMeasureCycleHandleWithNoTableRow()
    {
        var machine = Write();
        var path = MeasuresPath(machine);
        var measure = Measures(machine)[0];
        var artifacts = Artifacts(machine);
        artifacts[path] = CanonicalJson.WriteCompact(ImmutableArray.Create(new ScopeMeasures(
            measure.Scope, measure.Entity, measure.FanIn, measure.FanOut, measure.CrossComponentEdges,
            [new CycleHandle("zz")], measure.ReverseImpact, measure.Gaps)));
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "STO-05")]
    public void Read_RejectsDuplicateKeysInTheEntityTable()
    {
        var machine = Write();
        var path = TablePath(machine, "entities");
        var artifacts = Artifacts(machine);
        artifacts[path] = CanonicalJson.WriteCompact(ImmutableArray.Create("entity:a", "entity:a", "entity:c"));
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "STO-05")]
    public void Read_RejectsAMissingCycleTable()
    {
        var machine = Write();
        var path = TablePath(machine, "cycles");
        var artifacts = Artifacts(machine);
        artifacts.Remove(path);
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "STO-07")]
    public void Write_SameModelProducesIdenticalTableBytes()
    {
        var first = Write();
        var second = Write();
        foreach (var name in new[] { "entities", "variants", "cycles" })
        {
            Assert.True(Artifact(first, TablePath(first, name)).Payload.AsSpan()
                .SequenceEqual(Artifact(second, TablePath(second, name)).Payload.AsSpan()), name);
        }
    }

    private static ImmutableArray<string> Table(MachineArtifactSet machine, string name) =>
        CanonicalJson.Read<ImmutableArray<string>>(Artifact(machine, TablePath(machine, name)).Payload.AsSpan());

    private static string TablePath(MachineArtifactSet machine, string name) =>
        Assert.Single(machine.Artifacts, artifact => artifact.Path.Value.EndsWith($"/tables/{name}.000000.json", StringComparison.Ordinal)).Path.Value;

    private static Dictionary<string, ImmutableArray<byte>> Artifacts(MachineArtifactSet machine) =>
        machine.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);

    private static DependencyPayload Payload(MachineArtifactSet machine) =>
        CanonicalJson.Read<DependencyPayload>(Artifact(machine, DependencyPath(machine)).Payload.AsSpan());

    private static ImmutableArray<ScopeMeasures> Measures(MachineArtifactSet machine) =>
        CanonicalJson.Read<ImmutableArray<ScopeMeasures>>(Artifact(machine, MeasuresPath(machine)).Payload.AsSpan());

    private static string DependencyPath(MachineArtifactSet machine) =>
        Assert.Single(machine.Artifacts, artifact => artifact.Path.Value.EndsWith("/measures/dependencies.000000.json", StringComparison.Ordinal)).Path.Value;

    private static string MeasuresPath(MachineArtifactSet machine) =>
        Assert.Single(machine.Artifacts, artifact => artifact.Path.Value.EndsWith("/measures/summary.json", StringComparison.Ordinal)).Path.Value;

    private static PlannedArtifact Artifact(MachineArtifactSet machine, string path) =>
        Assert.Single(machine.Artifacts, artifact => artifact.Path.Value == path);

    private static MachineArtifactSet Write() => MachineArtifactWriter.Write(Model(), includeTests: false);

    private static RetrievalModel Model()
    {
        var solution = CanonicalIdentity.CreateSolution("app", "src/App.sln");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var span = new SourceSpan(1, 1, 1, 1);
        var facts = new RetainedGraph(
            [],
            [new FactualRelation("relation:a", "entity:a", "entity:b", "contract", ["evidence:b"])],
            [],
            [new EvidenceRecord("evidence:b", CanonicalIdentity.CreateDocumentKey(solution, "A.cs"), variant, span, "digest-b")],
            [],
            new RetentionMeasurements(1, 0));
        var dependency = new AggregatedDependency(
            AggregationScope.Component,
            new EntityHandle("entity:a"),
            new EntityHandle("entity:b"),
            DependencyCategory.Contract,
            DependencyNature.Direct,
            1,
            [new VariantHandle("variant:v")],
            [new RelationHandle("relation:a")],
            [new EvidenceHandle("evidence:b")]);
        var measure = new ScopeMeasures(
            AggregationScope.Component,
            new EntityHandle("entity:a"),
            1,
            1,
            0,
            [new CycleHandle("cycle:a")],
            [new ImpactTarget(new EntityHandle("entity:c"), 1)],
            new GapCounts(0, 0, 0));
        return new RetrievalModel([new SolutionRetrievalModel(solution, [new EntityHandle("entity:a")], [dependency], [measure], facts)]);
    }
}
