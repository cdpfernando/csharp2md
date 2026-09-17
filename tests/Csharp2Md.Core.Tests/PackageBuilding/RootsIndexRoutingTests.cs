using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;
using Csharp2Md.Core.Publication.Certification;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class RootsIndexRoutingTests
{
    [Fact]
    [Trait("Requirement", "PKG-01")]
    [Trait("Requirement", "NAV-01")]
    public void Write_ManifestDeclaresRootsByEntryPathAndCountInsteadOfARowPerRoot()
    {
        var machine = Write(rootCount: 3);
        var solution = Assert.Single(machine.Manifest.Solutions);

        Assert.Equal(IndexPath(machine), solution.Roots.EntryPath);
        Assert.Equal(3, solution.Roots.Count);
        var manifestText = Encoding.UTF8.GetString(Artifact(machine, "manifest.json").Payload.AsSpan());
        Assert.DoesNotContain("component:", manifestText, StringComparison.Ordinal);
        Assert.DoesNotContain("markdown/components/", manifestText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void Write_RootsIndexCarriesOneSortedRowPerRootWithItsOrdinalHandle()
    {
        var index = Index(Write(rootCount: 3));

        Assert.Equal(
            ["component:0000", "component:0001", "component:0002"],
            index.Roots.Select(root => root.DisplayName));
        Assert.Equal(
            Enumerable.Range(0, 3).Select(ordinal => LocalTableBuilder.HandleForOrdinal(ordinal)),
            index.Roots.Select(root => root.Handle));
    }

    [Fact]
    [Trait("Requirement", "NAV-04")]
    public void Write_RootsIndexDeclaresTheCitationArtifactAndMarkdownPrefixOnce()
    {
        var machine = Write(rootCount: 3);
        var index = Index(machine);
        var prefix = $"solutions/{Assert.Single(machine.Manifest.Solutions).Id.Value}";
        var indexText = Encoding.UTF8.GetString(Artifact(machine, IndexPath(machine)).Payload.AsSpan());

        Assert.Equal($"{prefix}/graph/entities.000000.json", index.CitationArtifact);
        Assert.Equal($"{prefix}/markdown/components/", index.MarkdownPrefix);
        Assert.Equal($"{prefix}/graph/entities.000000.json#1", index.MachineCitation(index.Roots[1].Handle));
        Assert.Equal($"{prefix}/markdown/components/1.md", index.MarkdownPath(index.Roots[1].Handle));
        Assert.Equal(1, Occurrences(indexText, "markdown/components/"));
        Assert.Equal(1, Occurrences(indexText, "graph/entities.000000.json"));
    }

    [Fact]
    [Trait("Requirement", "NAV-04")]
    public void Write_EveryDerivedRootPageIsAWrittenArtifact()
    {
        var model = Model(rootCount: 3);
        var machine = MachineArtifactWriter.Write(model, includeTests: false);
        var markdown = MarkdownRenderer.Render(model, machine.Manifest).Select(artifact => artifact.Path.Value).ToArray();
        var index = Index(machine);

        Assert.All(index.Roots, root => Assert.Contains(index.MarkdownPath(root.Handle), markdown));
    }

    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void Read_RestoresEveryRootFromTheDeclaredIndex()
    {
        var solution = Assert.Single(RetrievalModelReader.Read(Artifacts(Write(rootCount: 3))).Solutions);

        Assert.Equal(
            ["component:0000", "component:0001", "component:0002"],
            solution.Roots.Select(root => root.Value));
    }

    [Fact]
    [Trait("Requirement", "STO-05")]
    public void Read_RejectsARootsIndexWhoseRowCountDisagreesWithTheManifest()
    {
        var machine = Write(rootCount: 3);
        var artifacts = Artifacts(machine);
        var index = Index(machine);
        artifacts[IndexPath(machine)] = CanonicalJson.WriteCompact(index with { Roots = index.Roots.RemoveAt(2) });

        Assert.Equal(
            IndexPath(machine),
            Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact]
    [Trait("Requirement", "STO-05")]
    public void Read_RejectsARootsIndexThatIsNotOrdinalSortedByDisplayName()
    {
        var machine = Write(rootCount: 3);
        var artifacts = Artifacts(machine);
        var index = Index(machine);
        artifacts[IndexPath(machine)] = CanonicalJson.WriteCompact(index with
        {
            Roots = [index.Roots[0] with { DisplayName = "component:0002" }, index.Roots[1], index.Roots[2]],
        });

        Assert.Equal(
            IndexPath(machine),
            Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact]
    [Trait("Requirement", "STO-05")]
    public void Read_RejectsARootHandleThatIsNotItsOrdinal()
    {
        var machine = Write(rootCount: 3);
        var artifacts = Artifacts(machine);
        var index = Index(machine);
        artifacts[IndexPath(machine)] = CanonicalJson.WriteCompact(index with
        {
            Roots = index.Roots.SetItem(1, index.Roots[1] with { Handle = "zz" }),
        });

        Assert.Equal(
            IndexPath(machine),
            Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact]
    [Trait("Requirement", "STO-05")]
    public void Read_RejectsAMissingRootsIndex()
    {
        var machine = Write(rootCount: 3);
        var artifacts = Artifacts(machine);
        artifacts.Remove(IndexPath(machine));

        Assert.Equal(
            IndexPath(machine),
            Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(400)]
    [Trait("Requirement", "NAV-06")]
    [Trait("Requirement", "NAV-07")]
    public void Certify_LocateJourney_ReachesARootPageInThreeReadsWithinTwelveThousandTokens(int rootCount)
    {
        using var package = Package(rootCount);

        var journey = Locate(JourneyCertifier.Certify(package.Path));

        Assert.Equal(JourneyCertificationStatus.Passed, journey.Status);
        var measurement = Measurement(journey);
        Assert.Equal(3, measurement.Reads);
        Assert.InRange(measurement.Tokens, 1, 12_000);
    }

    // The theory above only ever emits `component:` roots, so it measures NAV-06's component path. NAV-07 is
    // about "outra raiz suportada" - the other three root kinds PKG-02 names - and pins a separate, looser
    // pair of bounds. Without a non-component root nothing measured those 8 reads or those 12,000 tokens.
    [Theory]
    [InlineData("deployment", 3)]
    [InlineData("deployment", 400)]
    [InlineData("entrypoint", 3)]
    [InlineData("entrypoint", 400)]
    [InlineData("boundary", 3)]
    [InlineData("boundary", 400)]
    [Trait("Requirement", "NAV-07")]
    public void Certify_LocateJourney_ReachesANonComponentRootWithinEightReadsAndTwelveThousandTokens(string kind, int rootCount)
    {
        using var package = Package(rootCount, kind);

        var journey = Locate(JourneyCertifier.Certify(package.Path));

        Assert.Equal(JourneyCertificationStatus.Passed, journey.Status);
        var measurement = Measurement(journey);
        Assert.InRange(measurement.Reads, 1, 8);
        Assert.InRange(measurement.Tokens, 1, 12_000);
    }

    [Fact]
    [Trait("Requirement", "CRT-01")]
    public void Certify_LocateJourney_FailsWhenTheDerivedRootPageIsMissing()
    {
        using var package = Package(rootCount: 3);
        var machine = Write(rootCount: 3);
        var index = Index(machine);
        File.Delete(Path.Combine(package.Path, index.MarkdownPath(index.Roots[0].Handle).Replace('/', Path.DirectorySeparatorChar)));

        var journey = Locate(JourneyCertifier.Certify(package.Path));

        Assert.Equal(JourneyCertificationStatus.Failed, journey.Status);
        Assert.Equal("missing-terminal", journey.Detail);
    }

    [Fact]
    [Trait("Requirement", "STO-07")]
    public void Write_SameModelProducesIdenticalRootsIndexAndManifestBytes()
    {
        var first = Write(rootCount: 3);
        var second = Write(rootCount: 3);

        Assert.True(Artifact(first, IndexPath(first)).Payload.AsSpan()
            .SequenceEqual(Artifact(second, IndexPath(second)).Payload.AsSpan()));
        Assert.True(Artifact(first, "manifest.json").Payload.AsSpan()
            .SequenceEqual(Artifact(second, "manifest.json").Payload.AsSpan()));
    }

    private static JourneyCertification Locate(PackageCertification certification) =>
        Assert.Single(Assert.Single(certification.Solutions).Journeys, journey => journey.Kind == JourneyKind.Locate);

    private static (int Reads, long Tokens) Measurement(JourneyCertification journey)
    {
        var values = journey.Detail.Split(';')
            .Select(part => part.Split(':', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        return (
            int.Parse(values["reads"], System.Globalization.CultureInfo.InvariantCulture),
            long.Parse(values["tokens"], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static TempPackage Package(int rootCount) => Package(rootCount, "component");

    private static TempPackage Package(int rootCount, string kind)
    {
        var package = new TempPackage();
        foreach (var artifact in PackageBuilder.Build(Model(rootCount, kind)).Artifacts)
        {
            package.Write(artifact.Path.Value, artifact.Payload);
        }

        return package;
    }

    private static RootsIndexData Index(MachineArtifactSet machine) =>
        CanonicalJson.Read<RootsIndexData>(Artifact(machine, IndexPath(machine)).Payload.AsSpan());

    private static string IndexPath(MachineArtifactSet machine) =>
        Assert.Single(machine.Manifest.Solutions).Roots.EntryPath;

    private static PlannedArtifact Artifact(MachineArtifactSet machine, string path) =>
        Assert.Single(machine.Artifacts, artifact => artifact.Path.Value == path);

    private static Dictionary<string, ImmutableArray<byte>> Artifacts(MachineArtifactSet machine) =>
        machine.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);

    private static MachineArtifactSet Write(int rootCount) =>
        MachineArtifactWriter.Write(Model(rootCount), includeTests: false);

    private static RetrievalModel Model(int rootCount) => Model(rootCount, "component");

    private static RetrievalModel Model(int rootCount, string kind)
    {
        var solution = CanonicalIdentity.CreateSolution("app", "src/App.sln");
        var roots = Enumerable.Range(0, rootCount)
            .Select(ordinal => new EntityHandle($"{kind}:{ordinal:D4}"))
            .ToImmutableArray();
        return new RetrievalModel([new SolutionRetrievalModel(solution, roots, [], [])]);
    }

    private sealed class TempPackage : IDisposable
    {
        internal TempPackage()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csharp2md-roots-index-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        internal void Write(string relativePath, ImmutableArray<byte> bytes)
        {
            var file = System.IO.Path.Combine(Path, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, bytes.ToArray());
        }

        public void Dispose() => TempPath.TryDelete(Path);
    }
}
