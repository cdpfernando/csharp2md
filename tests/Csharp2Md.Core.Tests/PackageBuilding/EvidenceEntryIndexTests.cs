using System.Text;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;
using Csharp2Md.Core.Publication.Certification;

namespace Csharp2Md.Core.Tests.PackageBuilding;

public sealed class EvidenceEntryIndexTests
{
    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void Write_EvidenceIndexCarriesShardPointersInsteadOfTheEvidencePayload()
    {
        var machine = Write(evidenceCount: 3);

        var index = Index(machine);
        var indexText = Encoding.UTF8.GetString(Artifact(machine, IndexPath(machine)).Payload.AsSpan());
        var shard = Assert.Single(index.Shards);
        Assert.Equal($"{Prefix(machine)}/tables/evidence.000000.json", shard.ArtifactPath);
        Assert.Equal(0, shard.FirstOrdinal);
        Assert.Equal(3, shard.Count);
        Assert.DoesNotContain("content_digest", indexText, StringComparison.Ordinal);
        Assert.DoesNotContain("document_canonical_key", indexText, StringComparison.Ordinal);
        Assert.Equal(3, Records(machine, shard.ArtifactPath).Length);
    }

    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void Write_EveryEvidenceHandleResolvesThroughTheIndexToItsOwnRecord()
    {
        var machine = Write(evidenceCount: 400);
        var index = Index(machine);
        var keys = Payload(machine).Evidence;

        for (var ordinal = 0; ordinal < keys.Length; ordinal++)
        {
            var shard = Assert.Single(
                index.Shards,
                entry => ordinal >= entry.FirstOrdinal && ordinal < entry.FirstOrdinal + entry.Count);
            var record = Records(machine, shard.ArtifactPath)[ordinal - shard.FirstOrdinal];
            Assert.Equal(keys[ordinal], record.CanonicalKey);
            Assert.Equal(LocalTableBuilder.HandleForOrdinal(ordinal), Handles(keys).Resolve(record.CanonicalKey).Value);
            Assert.Equal($"digest-{ordinal:D4}", record.ContentDigest);
        }
    }

    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void Write_ShardsPartitionTheEvidenceTableIntoContiguousOrdinalRanges()
    {
        var machine = Write(evidenceCount: 400);
        var index = Index(machine);

        Assert.True(index.Shards.Length > 1, "400 evidence records must not fit a single 64 KiB shard.");
        Assert.Equal(
            Enumerable.Range(0, index.Shards.Length).Select(ordinal => $"{Prefix(machine)}/tables/evidence.{ordinal:D6}.json"),
            index.Shards.Select(shard => shard.ArtifactPath));
        Assert.Equal(0, index.Shards[0].FirstOrdinal);
        Assert.All(
            index.Shards.Skip(1).Select((shard, position) => (shard, previous: index.Shards[position])),
            pair => Assert.Equal(pair.previous.FirstOrdinal + pair.previous.Count, pair.shard.FirstOrdinal));
        Assert.Equal(400, index.Shards.Sum(shard => shard.Count));
    }

    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void Reader_RejectsAnEvidenceIndexPointingToAMissingShard()
    {
        var machine = Write(evidenceCount: 3);
        var artifacts = Artifacts(machine);
        var index = Index(machine);
        var missing = $"{Prefix(machine)}/tables/evidence.000009.json";
        artifacts[IndexPath(machine)] = CanonicalJson.Write(new EvidenceIndexData([index.Shards[0] with { ArtifactPath = missing }]));

        Assert.Equal(
            missing,
            Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void Reader_RejectsAShardWhoseDeclaredCountDisagreesWithItsRows()
    {
        var machine = Write(evidenceCount: 3);
        var artifacts = Artifacts(machine);
        var index = Index(machine);
        artifacts[IndexPath(machine)] = CanonicalJson.Write(new EvidenceIndexData([index.Shards[0] with { Count = 2 }]));

        Assert.Equal(
            index.Shards[0].ArtifactPath,
            Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Fact]
    [Trait("Requirement", "NAV-01")]
    public void Reader_RejectsAnEvidenceIndexWhoseShardRangesSkipAnOrdinal()
    {
        var machine = Write(evidenceCount: 3);
        var artifacts = Artifacts(machine);
        var index = Index(machine);
        artifacts[IndexPath(machine)] = CanonicalJson.Write(new EvidenceIndexData([index.Shards[0] with { FirstOrdinal = 1 }]));

        Assert.Equal(
            IndexPath(machine),
            Assert.Throws<PackageCorruptionException>(() => RetrievalModelReader.Read(artifacts)).Artifact);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(400)]
    [Trait("Requirement", "NAV-10")]
    [Trait("Requirement", "NAV-01")]
    public void Certify_EvidenceJourney_ResolvesASelectedRecordWithinTwelveReadsAndTwentyFiveThousandTokens(int evidenceCount)
    {
        using var package = Package(evidenceCount);

        var journey = Evidence(JourneyCertifier.Certify(package.Path));

        Assert.Equal(JourneyCertificationStatus.Passed, journey.Status);
        var measurement = Measurement(journey);
        Assert.Equal(3, measurement.Reads);
        Assert.InRange(measurement.Tokens, 1, 25_000);
    }

    [Fact]
    [Trait("Requirement", "CRT-01")]
    public void Certify_EvidenceJourney_FailsWhenTheSelectedShardIsMissing()
    {
        using var package = Package(evidenceCount: 3);
        var shard = Index(Write(evidenceCount: 3)).Shards[0].ArtifactPath;
        File.Delete(Path.Combine(package.Path, shard.Replace('/', Path.DirectorySeparatorChar)));

        var journey = Evidence(JourneyCertifier.Certify(package.Path));

        Assert.Equal(JourneyCertificationStatus.Failed, journey.Status);
        Assert.Equal("missing-terminal", journey.Detail);
    }

    [Fact]
    [Trait("Requirement", "CRT-01")]
    public void Certify_SolutionWithoutRetainedEvidence_RecordsNotApplicable()
    {
        using var package = Package(evidenceCount: 0);

        var journey = Evidence(JourneyCertifier.Certify(package.Path));

        Assert.Equal(JourneyCertificationStatus.NotApplicable, journey.Status);
        Assert.Equal("not_applicable:no-evidence-index", journey.Detail);
    }

    private static JourneyCertification Evidence(PackageCertification certification) =>
        Assert.Single(Assert.Single(certification.Solutions).Journeys, journey => journey.Kind == JourneyKind.EvidenceDisposition);

    private static (int Reads, long Tokens) Measurement(JourneyCertification journey)
    {
        var values = journey.Detail.Split(';')
            .Select(part => part.Split(':', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        return (
            int.Parse(values["reads"], System.Globalization.CultureInfo.InvariantCulture),
            long.Parse(values["tokens"], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static TempPackage Package(int evidenceCount)
    {
        var package = new TempPackage();
        foreach (var artifact in PackageBuilder.Build(Model(evidenceCount)).Artifacts)
        {
            package.Write(artifact.Path.Value, artifact.Payload);
        }

        return package;
    }

    private static EvidenceIndexData Index(MachineArtifactSet machine) =>
        CanonicalJson.Read<EvidenceIndexData>(Artifact(machine, IndexPath(machine)).Payload.AsSpan());

    private static ImmutableArray<EvidenceRecord> Records(MachineArtifactSet machine, string path) =>
        CanonicalJson.Read<ImmutableArray<EvidenceRecord>>(Artifact(machine, path).Payload.AsSpan());

    private static DependencyPayload Payload(MachineArtifactSet machine) =>
        CanonicalJson.Read<DependencyPayload>(
            Assert.Single(machine.Artifacts, artifact => artifact.Path.Value.EndsWith("/measures/dependencies.000000.json", StringComparison.Ordinal)).Payload.AsSpan());

    private static LocalTable Handles(ImmutableArray<string> evidenceKeys) =>
        LocalTableBuilder.Build(CanonicalIdentity.CreateSolution("app", "src/App.sln").CanonicalKey, evidenceKeys);

    private static string Prefix(MachineArtifactSet machine) =>
        $"solutions/{Assert.Single(machine.Manifest.Solutions).Id.Value}";

    private static string IndexPath(MachineArtifactSet machine) =>
        Assert.Single(Assert.Single(machine.Manifest.Solutions).Indexes, index => index.Kind == NavigationIndexKind.Evidence).EntryPath;

    private static PlannedArtifact Artifact(MachineArtifactSet machine, string path) =>
        Assert.Single(machine.Artifacts, artifact => artifact.Path.Value == path);

    private static Dictionary<string, ImmutableArray<byte>> Artifacts(MachineArtifactSet machine) =>
        machine.Artifacts.ToDictionary(artifact => artifact.Path.Value, artifact => artifact.Payload, StringComparer.Ordinal);

    private static MachineArtifactSet Write(int evidenceCount) =>
        MachineArtifactWriter.Write(Model(evidenceCount), includeTests: false);

    private static RetrievalModel Model(int evidenceCount)
    {
        var solution = CanonicalIdentity.CreateSolution("app", "src/App.sln");
        var variant = CanonicalIdentity.CreateVariant("net10.0", "Release", [], "ci");
        var span = new SourceSpan(1, 1, 1, 1);
        var evidence = Enumerable.Range(0, evidenceCount)
            .Select(ordinal => new EvidenceRecord(
                $"evidence:{ordinal:D4}",
                CanonicalIdentity.CreateDocumentKey(solution, $"src/Component/Handlers/Document{ordinal:D4}.cs"),
                variant,
                span,
                $"digest-{ordinal:D4}"))
            .ToImmutableArray();
        var root = new EntityHandle("component:shared");
        return new RetrievalModel([
            new SolutionRetrievalModel(
                solution,
                [root],
                [Dependency(root, DependencyCategory.Contract, "contract"), Dependency(root, DependencyCategory.Persistence, "database")],
                [new ScopeMeasures(AggregationScope.Component, root, 0, 2, 1, [], [new ImpactTarget(new EntityHandle("component:caller"), 1)], new GapCounts(0, 0, 0))],
                new RetainedGraph([], [], [], evidence, [], new RetentionMeasurements(evidenceCount, 0)))]);
    }

    private static AggregatedDependency Dependency(EntityHandle root, DependencyCategory category, string target) =>
        new(AggregationScope.Component, root, new EntityHandle($"component:{target}"), category, DependencyNature.Direct, 1, [], [], []);

    private sealed class TempPackage : IDisposable
    {
        internal TempPackage()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "csharp2md-evidence-index-" + Guid.NewGuid().ToString("N"));
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
