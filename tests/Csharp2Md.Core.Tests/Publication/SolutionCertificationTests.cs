using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.Publication;
using Csharp2Md.Core.Publication.Certification;

namespace Csharp2Md.Core.Tests.Publication;

public sealed class SolutionCertificationTests
{
    [Fact]
    [Trait("Requirement", "CRT-01")]
    [Trait("Requirement", "STO-04")]
    public void Certify_TwoSolutions_ProducesFourJourneysForEachSolution()
    {
        using var package = Package();

        var certification = JourneyCertifier.Certify(package.Path);

        Assert.Equal(2, certification.Solutions.Length);
        Assert.All(certification.Solutions, solution => Assert.Equal(4, solution.Journeys.Length));
        Assert.Equal(2, certification.Solutions.Select(solution => solution.SolutionId).Distinct().Count());
        Assert.All(
            certification.Solutions,
            solution => Assert.Equal(
                [JourneyKind.Locate, JourneyKind.FollowFlow, JourneyKind.ReverseImpact, JourneyKind.EvidenceDisposition],
                solution.Journeys.Select(journey => journey.Kind).Order().ToArray()));
    }

    [Fact]
    [Trait("Requirement", "CRT-01")]
    [Trait("Requirement", "EDG-02")]
    public void Certify_EachJourney_RestartsReadAndTokenMeasurements()
    {
        using var package = Package();

        var solution = JourneyCertifier.Certify(package.Path).Solutions[0];
        using var reader = PackageReader.Open(package.Path);
        var manifestSolution = Assert.Single(
            reader.Manifest.Solutions,
            candidate => candidate.Id == solution.SolutionId);
        var evidencePath = Assert.Single(
            manifestSolution.Indexes,
            index => index.Kind == NavigationIndexKind.Evidence).EntryPath;
        var expectedEvidenceBytes = new FileInfo(Path.Combine(package.Path, "manifest.json")).Length
            + new FileInfo(Path.Combine(package.Path, evidencePath.Replace('/', Path.DirectorySeparatorChar))).Length;
        var evidenceMeasurement = Measurement(solution, JourneyKind.EvidenceDisposition);

        Assert.Equal(3, Reads(solution, JourneyKind.Locate));
        Assert.Equal(4, Reads(solution, JourneyKind.FollowFlow));
        Assert.Equal(3, Reads(solution, JourneyKind.ReverseImpact));
        Assert.Equal(2, Reads(solution, JourneyKind.EvidenceDisposition));
        Assert.Equal(expectedEvidenceBytes, evidenceMeasurement.Bytes);
        Assert.Equal(
            (long)Math.Ceiling(expectedEvidenceBytes / PackageManifest.TokenDivisorValue),
            evidenceMeasurement.Tokens);
        Assert.All(solution.Journeys, journey => Assert.Equal(JourneyCertificationStatus.Passed, journey.Status));
        Assert.All(solution.Journeys, journey => Assert.Contains("bytes:", journey.Detail, StringComparison.Ordinal));
        Assert.All(solution.Journeys, journey => Assert.Contains("tokens:", journey.Detail, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "CRT-01")]
    [Trait("Requirement", "STO-04")]
    public void Certify_CorruptionOnlyInSecondSolution_FailsThatSolutionWithoutMaskingIt()
    {
        using var package = Package();
        using var reader = PackageReader.Open(package.Path);
        var first = reader.Manifest.Solutions[0];
        var second = reader.Manifest.Solutions[1];
        var corruptedPath = Assert.Single(
            second.Indexes,
            index => index.Kind == NavigationIndexKind.Evidence).EntryPath;
        File.Delete(Path.Combine(package.Path, corruptedPath.Replace('/', Path.DirectorySeparatorChar)));

        var certification = JourneyCertifier.Certify(package.Path);

        var firstCertification = Assert.Single(certification.Solutions, result => result.SolutionId == first.Id);
        var secondCertification = Assert.Single(certification.Solutions, result => result.SolutionId == second.Id);
        Assert.Equal(
            JourneyCertificationStatus.Passed,
            Assert.Single(firstCertification.Journeys, journey => journey.Kind == JourneyKind.EvidenceDisposition).Status);
        Assert.Equal(
            JourneyCertificationStatus.Failed,
            Assert.Single(secondCertification.Journeys, journey => journey.Kind == JourneyKind.EvidenceDisposition).Status);
    }

    private static int Reads(SolutionCertification solution, JourneyKind kind) => Measurement(solution, kind).Reads;

    private static JourneyMeasurementValues Measurement(SolutionCertification solution, JourneyKind kind)
    {
        var detail = Assert.Single(solution.Journeys, journey => journey.Kind == kind).Detail;
        var values = detail.Split(';')
            .Select(value => value.Split(':', 2))
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
        return new JourneyMeasurementValues(
            int.Parse(values["reads"], System.Globalization.CultureInfo.InvariantCulture),
            long.Parse(values["bytes"], System.Globalization.CultureInfo.InvariantCulture),
            long.Parse(values["tokens"], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static TempPackage Package()
    {
        var package = new TempPackage();
        foreach (var artifact in PackageBuilder.Build(Model()).Artifacts)
        {
            package.Write(artifact.Path.Value, artifact.Payload);
        }

        return package;
    }

    private static RetrievalModel Model() => new([Solution("orders"), Solution("shipping")]);

    private static SolutionRetrievalModel Solution(string name)
    {
        var root = new EntityHandle("component:shared");
        return new SolutionRetrievalModel(
            CanonicalIdentity.CreateSolution(name, $"src/{name}.sln"),
            [root],
            [
                Dependency(root, DependencyCategory.Contract, "contract"),
                Dependency(root, DependencyCategory.Persistence, "database"),
                Dependency(root, DependencyCategory.Http, "external"),
            ],
            [new ScopeMeasures(
                AggregationScope.Component,
                root,
                fanIn: 0,
                fanOut: 3,
                crossComponentEdges: 1,
                [],
                [new ImpactTarget(new EntityHandle($"component:{name}-caller"), 1)],
                new GapCounts(0, 0, 0))],
            retainedGraph: null);
    }

    private static AggregatedDependency Dependency(
        EntityHandle root,
        DependencyCategory category,
        string target) =>
        new(
            AggregationScope.Component,
            root,
            new EntityHandle($"component:{target}"),
            category,
            DependencyNature.Direct,
            1,
            [],
            [],
            []);

    private sealed record JourneyMeasurementValues(int Reads, long Bytes, long Tokens);

    private sealed class TempPackage : IDisposable
    {
        internal TempPackage()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "csharp2md-solution-certification-" + Guid.NewGuid().ToString("N"));
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
