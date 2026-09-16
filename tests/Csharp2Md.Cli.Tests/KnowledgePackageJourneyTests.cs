using System.Text;
using System.Text.Json;

namespace Csharp2Md.Cli.Tests;

public sealed class KnowledgePackageJourneyTests : IClassFixture<KnowledgePackageJourneyTests.PackageRun>
{
    private readonly PackageRun _run;

    public KnowledgePackageJourneyTests(PackageRun run) => _run = run;

    [Fact]
    public void StartsAtRootManifest_WithoutEnumeratingPackageDirectories() =>
        Assert.Equal(["manifest.json"], _run.InitialReads);

    [Fact]
    public void Manifest_DeclaresOneSolutionWithAllEightIndexes() =>
        Assert.Equal(8, _run.Solution.GetProperty("indexes").GetArrayLength());

    [Fact]
    public void Manifest_DeclaresTheFourRequiredJourneys() =>
        Assert.Equal(
            ["evidence_disposition", "follow_flow", "locate", "reverse_impact"],
            _run.Solution.GetProperty("journeys").EnumerateArray().Select(static journey => journey.GetProperty("kind").GetString()!).Order().ToArray());

    [Fact]
    public void Roots_ContainArchitecturalEntryPoints() =>
        Assert.Contains(_run.Roots.EnumerateArray(), static root => root.GetString()!.Contains(":entrypoint:", StringComparison.Ordinal));

    [Fact]
    public void Roots_ContainComponentAndDeploymentUnit()
    {
        var roots = _run.Roots.EnumerateArray().Select(static root => root.GetString()!).ToArray();

        Assert.Contains(roots, static root => root.Contains(":component:", StringComparison.Ordinal));
        Assert.Contains(roots, static root => root.Contains(":deploymentunit:", StringComparison.Ordinal));
    }

    [Fact]
    public void HandAuthoredFixtureEdges_ProduceExpectedDependencyCategories()
    {
        var categories = _run.Dependencies.EnumerateArray().Select(static edge => edge.GetProperty("category").GetInt32()).ToHashSet();

        Assert.True(categories.IsSupersetOf(ExpectedCategories));
    }

    [Fact]
    public void Dependencies_AggregateRepeatedConfirmedOccurrences() =>
        Assert.Contains(_run.Dependencies.EnumerateArray(), static edge => edge.GetProperty("occurrence_count").GetInt32() > 1);

    [Fact]
    public void Dependencies_ReferenceEvidenceAndRelationsWithoutDuplicatingPayloads()
    {
        var dependency = _run.Dependencies.EnumerateArray()
            .First(static edge => edge.GetProperty("occurrence_count").GetInt32() > 1);

        Assert.NotEmpty(dependency.GetProperty("relations").EnumerateArray());
        Assert.NotEmpty(dependency.GetProperty("evidence").EnumerateArray());
    }

    [Fact]
    public void Measures_ExposeFanInFanOutAndReverseImpact()
    {
        var measure = _run.Measures.EnumerateArray().First(static item =>
            item.GetProperty("fan_out").GetInt32() > 0
            && item.GetProperty("reverse_impact").GetArrayLength() > 0);

        Assert.True(measure.GetProperty("fan_in").GetInt32() >= 0);
        Assert.NotEmpty(measure.GetProperty("reverse_impact").EnumerateArray());
    }

    [Fact]
    public void Certification_CompletesLocateJourneyWithinComponentBudget() =>
        AssertBudget("locate", maximumReads: 5, maximumTokens: 12_000);

    [Fact]
    public void Certification_CompletesFlowJourneyWithinBudget() =>
        AssertBudget("follow_flow", maximumReads: 32, maximumTokens: 125_000);

    [Fact]
    public void Certification_CompletesReverseImpactJourneyWithinBudget() =>
        AssertBudget("reverse_impact", maximumReads: 32, maximumTokens: 125_000);

    [Fact]
    public void Certification_CompletesEvidenceJourneyWithinBudget() =>
        AssertBudget("evidence_disposition", maximumReads: 12, maximumTokens: 25_000);

    [Fact]
    public async Task ImmediateValidate_ReusesTheCommittedPackageInterpretation()
    {
        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(["validate", "--package", _run.Directory]);

        Assert.Equal(0, exitCode);
        Assert.Contains("valid", stdout, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(string.Empty, stderr);
    }

    [Fact]
    public void StandardPackage_ExcludesTestsSecretsAndAbsoluteFixtureValues()
    {
        var text = _run.ReadAllDeclaredText();

        Assert.DoesNotContain("Acme.Shipping.Tests", text, StringComparison.Ordinal);
        Assert.DoesNotContain("appsettings-fixture-secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("inline-fixture-secret", text, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\fixture\\synthetic-output", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_RecordsTheDefaultNoTestsPolicy() =>
        Assert.False(_run.Manifest.GetProperty("include_tests").GetBoolean());

    private void AssertBudget(string kind, int maximumReads, int maximumTokens)
    {
        var journey = Assert.Single(_run.Certification.GetProperty("solutions")[0].GetProperty("journeys")
            .EnumerateArray(), candidate => candidate.GetProperty("kind").GetString() == kind);
        var detail = journey.GetProperty("detail").GetString()!;
        var values = detail.Split(';').Select(static part => part.Split(':')).ToDictionary(static pair => pair[0], static pair => int.Parse(pair[1]));

        Assert.Equal(0, journey.GetProperty("status").GetInt32());
        Assert.InRange(values["reads"], 1, maximumReads);
        Assert.InRange(values["tokens"], 1, maximumTokens);
    }

    private static readonly HashSet<int> ExpectedCategories = [3, 4, 5, 6, 7];

    public sealed class PackageRun : IAsyncLifetime
    {
        private readonly List<string> _reads = [];
        private JsonDocument? _manifest;
        private JsonDocument? _generationManifest;
        private JsonDocument? _roots;
        private JsonDocument? _dependencies;
        private JsonDocument? _measures;
        private JsonDocument? _certification;

        internal string Directory { get; private set; } = null!;
        internal IReadOnlyList<string> InitialReads => _reads.Take(1).ToArray();
        internal JsonElement Manifest => _generationManifest!.RootElement;
        internal JsonElement Solution => Manifest.GetProperty("solutions")[0];
        internal JsonElement Roots => _roots!.RootElement;
        internal JsonElement Dependencies => _dependencies!.RootElement.GetProperty("dependencies");
        internal JsonElement Measures => _measures!.RootElement;
        internal JsonElement Certification => _certification!.RootElement;

        public async Task InitializeAsync()
        {
            Directory = CliTestPaths.UniqueOutputPath();
            var solution = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution", "Acme.Journey.slnx");
            var (exitCode, _, stderr) = await CliInvoke.RunAsync(["analyze", "--solution", solution, "--output", Directory]);
            Assert.Equal(0, exitCode);

            _manifest = ReadDocument("manifest.json");
            var generation = _manifest.RootElement.GetProperty("generation").GetString()!;
            _generationManifest = ReadDocument($"generations/{generation}/manifest.json");
            _reads.Clear();
            _reads.Add("manifest.json");

            var prefix = $"generations/{generation}/";
            _roots = ReadDocument(prefix + IndexPath("roots"));
            var solutionPrefix = prefix + "solutions/" + Solution.GetProperty("id").GetString() + "/";
            _dependencies = ReadDocument(solutionPrefix + "measures/dependencies.000000.json");
            _measures = ReadDocument(solutionPrefix + "measures/summary.json");
            _certification = ReadDocument(prefix + "certification.json");
        }

        public Task DisposeAsync()
        {
            _manifest?.Dispose();
            _generationManifest?.Dispose();
            _roots?.Dispose();
            _dependencies?.Dispose();
            _measures?.Dispose();
            _certification?.Dispose();
            CliTestPaths.TryDeleteDirectory(Directory);
            return Task.CompletedTask;
        }

        internal string ReadAllDeclaredText()
        {
            var generation = _manifest!.RootElement.GetProperty("generation").GetString()!;
            var files = new[]
            {
                "manifest.json",
                $"generations/{generation}/manifest.json",
                $"generations/{generation}/certification.json",
                $"generations/{generation}/measurements.json",
                $"generations/{generation}/markdown/index.md",
            }.Concat(Solution.GetProperty("indexes").EnumerateArray().Select(index => $"generations/{generation}/{index.GetProperty("entry_path").GetString()}"));
            return string.Join('\n', files.Distinct(StringComparer.Ordinal).Select(ReadText));
        }

        private string IndexPath(string kind) => Solution.GetProperty("indexes").EnumerateArray()
            .Single(index => index.GetProperty("kind").GetString() == kind).GetProperty("entry_path").GetString()!;

        private JsonDocument ReadDocument(string relativePath) => JsonDocument.Parse(ReadText(relativePath));

        private string ReadText(string relativePath)
        {
            _reads.Add(relativePath);
            return File.ReadAllText(Path.Combine(Directory, relativePath.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);
        }
    }
}
