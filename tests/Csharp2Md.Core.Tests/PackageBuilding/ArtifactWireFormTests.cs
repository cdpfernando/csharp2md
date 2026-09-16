using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class ArtifactWireFormTests
{
    [Fact] [Trait("Requirement", "STO-03")]
    public void Write_SerializesDependencyHandlesAsJsonStrings()
    {
        var machine = Write();
        var text = Text(machine, DependencyPath(machine));
        Assert.Contains("\"source\":\"entity:a\"", text, StringComparison.Ordinal);
        Assert.Contains("\"target\":\"entity:b\"", text, StringComparison.Ordinal);
        Assert.Contains("\"variants\":[\"variant:v\"]", text, StringComparison.Ordinal);
        Assert.Contains("\"relations\":[\"0\"]", text, StringComparison.Ordinal);
        Assert.Contains("\"evidence\":[\"0\"]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"value\"", text, StringComparison.Ordinal);
    }

    [Fact] [Trait("Requirement", "STO-03")]
    public void Write_SerializesRootEntityHandlesAsJsonStrings()
    {
        var machine = Write();
        var path = Assert.Single(machine.Artifacts, artifact => artifact.Path.Value.EndsWith("/graph/entities.000000.json", StringComparison.Ordinal)).Path.Value;
        var text = Text(machine, path);
        Assert.DoesNotContain("\"value\"", text, StringComparison.Ordinal);
        Assert.Contains("\"entity:a\"", text, StringComparison.Ordinal);
    }

    [Fact] [Trait("Requirement", "STO-03")]
    public void Write_SerializesMeasureEntityAndCycleHandlesAsJsonStrings()
    {
        var machine = Write();
        var text = Text(machine, MeasuresPath(machine));
        Assert.Contains("\"entity\":\"entity:a\"", text, StringComparison.Ordinal);
        Assert.Contains("\"cycles\":[\"cycle:a\"]", text, StringComparison.Ordinal);
        Assert.Contains("\"reverse_impact\":[{\"entity\":\"entity:b\",\"depth\":1}]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("{\"value\"", text, StringComparison.Ordinal);
    }

    [Fact] [Trait("Requirement", "STO-04")]
    public void Read_RestoresEveryHandleKindFromItsJsonString()
    {
        var solution = Assert.Single(RetrievalModelReader.Read(Artifacts(Write())).Solutions);
        Assert.Equal(["entity:a"], solution.Roots.Select(root => root.Value));
        var dependency = Assert.Single(solution.Dependencies);
        Assert.Equal("entity:a", dependency.Source.Value);
        Assert.Equal("entity:b", dependency.Target.Value);
        Assert.Equal(["variant:v"], dependency.Variants.Select(variant => variant.Value));
        Assert.Equal(["relation:a"], dependency.Relations.Select(relation => relation.Value));
        Assert.Equal(["evidence:b"], dependency.Evidence.Select(evidence => evidence.Value));
        var measure = Assert.Single(solution.Measures);
        Assert.Equal("entity:a", measure.Entity.Value);
        Assert.Equal(["cycle:a"], measure.Cycles.Select(cycle => cycle.Value));
        Assert.Equal("entity:b", Assert.Single(measure.ReverseImpact).Entity.Value);
    }

    [Fact] [Trait("Requirement", "STO-04")]
    public void Read_RejectsAHandleThatIsNotAJsonString()
    {
        var machine = Write();
        var path = MeasuresPath(machine);
        var artifacts = Artifacts(machine);
        artifacts[path] = Replace(artifacts[path], "\"entity\":\"entity:a\"", "\"entity\":1");
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "STO-04")]
    public void Read_RejectsABlankHandleString()
    {
        var machine = Write();
        var path = MeasuresPath(machine);
        var artifacts = Artifacts(machine);
        artifacts[path] = Replace(artifacts[path], "\"entity\":\"entity:a\"", "\"entity\":\" \"");
        Assert.Equal(path, Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact] [Trait("Requirement", "PKG-01")]
    public void Write_SummaryArtifactCarriesNoIndentation()
    {
        var machine = Write();
        var text = Text(machine, MeasuresPath(machine));
        Assert.DoesNotContain("\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("  ", text, StringComparison.Ordinal);
    }

    [Fact] [Trait("Requirement", "PKG-01")]
    public void Write_CompactSummaryRehydratesToTheSameMeasures()
    {
        var machine = Write();
        var measure = Assert.Single(CanonicalJson.Read<ImmutableArray<ScopeMeasures>>(Artifact(machine, MeasuresPath(machine)).Payload.AsSpan()));
        var expected = Model().Solutions[0].Measures[0];
        Assert.Equal(expected.Scope, measure.Scope);
        Assert.Equal(expected.Entity, measure.Entity);
        Assert.Equal(expected.FanIn, measure.FanIn);
        Assert.Equal(expected.FanOut, measure.FanOut);
        Assert.Equal(expected.CrossComponentEdges, measure.CrossComponentEdges);
        Assert.Equal(expected.Cycles.AsEnumerable(), measure.Cycles.AsEnumerable());
        Assert.Equal(expected.ReverseImpact.AsEnumerable(), measure.ReverseImpact.AsEnumerable());
        Assert.Equal(expected.Gaps, measure.Gaps);
    }

    [Fact] [Trait("Requirement", "STO-07")]
    public void Write_SameModelProducesIdenticalSummaryBytes()
    {
        var first = Write();
        var second = Write();
        Assert.True(Artifact(first, MeasuresPath(first)).Payload.AsSpan().SequenceEqual(Artifact(second, MeasuresPath(second)).Payload.AsSpan()));
    }

    private static ImmutableArray<byte> Replace(ImmutableArray<byte> payload, string original, string replacement)
    {
        var text = Encoding.UTF8.GetString(payload.AsSpan());
        Assert.Contains(original, text, StringComparison.Ordinal);
        return Encoding.UTF8.GetBytes(text.Replace(original, replacement, StringComparison.Ordinal)).ToImmutableArray();
    }

    private static Dictionary<string, ImmutableArray<byte>> Artifacts(MachineArtifactSet machine) =>
        machine.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);

    private static string Text(MachineArtifactSet machine, string path) =>
        Encoding.UTF8.GetString(Artifact(machine, path).Payload.AsSpan());

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
            [new ImpactTarget(new EntityHandle("entity:b"), 1)],
            new GapCounts(0, 0, 0));
        return new RetrievalModel([new SolutionRetrievalModel(solution, [new EntityHandle("entity:a")], [dependency], [measure], facts)]);
    }
}
