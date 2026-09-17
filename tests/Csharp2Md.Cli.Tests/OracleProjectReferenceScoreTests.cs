using System.Text.Json;

namespace Csharp2Md.Cli.Tests;

/// <summary>
/// The dependency ratchet. It analyses the six solutions of <c>fixtures/ArchitectureDependencyLab</c>
/// in one package and scores the Project-scope <c>ProjectReference</c> edges the generator publishes
/// against that corpus's oracle, per solution.
/// <para>
/// <b>Every baseline in <see cref="RecordedDefects"/> is a measured defect, not an expected outcome.</b>
/// The oracle names 87 <c>ProjectReference</c> edges across the six solutions. The generator states 40,
/// of which 11 are right and 29 are false positives -- self-edges and a fan-out from each API project to
/// projects it does not reference. <c>fixtures/SyntheticSolution</c> cannot see any of this: with two
/// entities a complete graph and a correct graph are the same graph.
/// </para>
/// <para>
/// Each case is a two-sided ratchet. Fewer correct edges or more false positives fail as a regression.
/// <b>An improvement fails too</b>, and says so: the baseline must be raised in the same commit that
/// fixes the generator, because a ratchet that silently absorbs progress stops measuring anything. The
/// failure message always carries the corpus target (87 correct, 0 false positives) so the distance to
/// a correct generator is printed, not inferred.
/// </para>
/// </summary>
public sealed class OracleProjectReferenceScoreTests : IClassFixture<ArchitectureDependencyLabPackage>
{
    private readonly ArchitectureDependencyLabPackage package;

    public OracleProjectReferenceScoreTests(ArchitectureDependencyLabPackage package) => this.package = package;

    /// <summary>
    /// Measured on 2026-09-17 against the vendored corpus. <c>CorrectToday</c> and
    /// <c>FalsePositivesToday</c> record how wrong the generator is right now; only <c>OracleEdges</c>
    /// states a truth, and it is read back from the oracle so the corpus cannot drift under the ratchet.
    /// </summary>
    internal static readonly ImmutableArray<OracleBaseline> RecordedDefects =
    [
        new("SistemaA", OracleEdges: 4, CorrectToday: 1, FalsePositivesToday: 3),
        new("SistemaB", OracleEdges: 18, CorrectToday: 3, FalsePositivesToday: 5),
        // SistemaC states nothing at all at Project scope, so its case can only fail upward -- on a new
        // false positive or on the fix. It cannot detect a regression; the other five carry that duty.
        new("SistemaC", OracleEdges: 1, CorrectToday: 0, FalsePositivesToday: 0),
        new("SistemaD", OracleEdges: 8, CorrectToday: 3, FalsePositivesToday: 3),
        new("SistemaE", OracleEdges: 28, CorrectToday: 2, FalsePositivesToday: 9),
        new("SistemaE.Copia", OracleEdges: 28, CorrectToday: 2, FalsePositivesToday: 9),
    ];

    public static TheoryData<string> Solutions =>
        new(RecordedDefects.Select(static defect => defect.Solution));

    [Theory]
    [MemberData(nameof(Solutions))]
    [Trait("Requirement", "DEP-01")]
    [Trait("Category", "OracleCorpus")]
    public void ProjectReferences_HoldTheRecordedDefectBaseline(string solution)
    {
        var baseline = RecordedDefects.Single(defect => defect.Solution == solution);
        var score = package.Score(solution);

        Assert.Equal(baseline.OracleEdges, score.OracleEdges);
        Assert.True(score.Correct >= baseline.CorrectToday, Verdict(score, baseline));
        Assert.True(score.FalsePositives <= baseline.FalsePositivesToday, Verdict(score, baseline));
        Assert.True(
            score.Correct == baseline.CorrectToday && score.FalsePositives == baseline.FalsePositivesToday,
            Verdict(score, baseline));
    }

    [Fact]
    [Trait("Requirement", "DEP-01")]
    [Trait("Category", "OracleCorpus")]
    public void Corpus_ScoresTheRecordedShareOfItsOracle()
    {
        var scores = RecordedDefects.Select(defect => package.Score(defect.Solution)).ToArray();
        var correct = scores.Sum(static score => score.Correct);
        var falsePositives = scores.Sum(static score => score.FalsePositives);
        var oracleEdges = scores.Sum(static score => score.OracleEdges);

        Assert.Equal(OracleCorpusEdges, oracleEdges);
        Assert.True(
            correct == BaselineCorrect && falsePositives == BaselineFalsePositives,
            $"The corpus baseline moved: {correct} correct and {falsePositives} false positives against a "
            + $"recorded {BaselineCorrect} correct and {BaselineFalsePositives} false positives. A correct "
            + $"generator states {oracleEdges} of {oracleEdges} with no false positive. Raise or lower the "
            + "baselines in this file in the same commit that moved them.");
    }

    /// <summary>
    /// Rules 2 and 3 of <c>oracle/README.md</c>: the five systems are independent, and the nested copy at
    /// <c>src/SistemaB/Copias/SistemaE.Copia</c> is neither merged into SistemaB nor into SistemaE. Both
    /// hold today, so this is a true invariant rather than a baseline -- it fails only on a new defect.
    /// </summary>
    [Fact]
    [Trait("Requirement", "DEP-02")]
    [Trait("Category", "OracleCorpus")]
    public void ProjectReferences_NeverNameAProjectFromAnotherSystem()
    {
        var oracle = ArchitectureDependencyLabPackage.ReadOracle();

        foreach (var defect in RecordedDefects)
        {
            var known = oracle[defect.Solution]
                .SelectMany(static edge => new[] { edge.Source, edge.Target })
                .ToHashSet(StringComparer.Ordinal);
            var foreign = package.StatedEdges(defect.Solution)
                .SelectMany(static edge => new[] { edge.Source, edge.Target })
                .Where(project => !known.Contains(project))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                foreign.Length == 0,
                $"{defect.Solution} states edges naming projects its oracle does not know: "
                + $"{string.Join(", ", foreign)}. oracle/README.md rule 2 keeps the systems independent and "
                + "rule 3 keeps SistemaE.Copia out of SistemaB and SistemaE.");
        }
    }

    internal const int OracleCorpusEdges = 87;
    private const int BaselineCorrect = 11;
    private const int BaselineFalsePositives = 29;

    private static string Verdict(SolutionScore score, OracleBaseline baseline) =>
        $"{score.Solution}: {score.Correct} correct (baseline {baseline.CorrectToday}) and "
        + $"{score.FalsePositives} false positives (baseline {baseline.FalsePositivesToday}) against "
        + $"{score.OracleEdges} oracle edges; the corpus target is {OracleCorpusEdges} correct and 0 false "
        + "positives. Fewer correct edges or more false positives is a regression; an improvement means "
        + "this baseline is stale and must be raised in the commit that improved it. False positives now: "
        + $"{Format(score.FalsePositiveEdges)}. Missing now: {Format(score.MissingEdges)}.";

    private static string Format(IReadOnlyCollection<ProjectEdge> edges) =>
        edges.Count == 0
            ? "none"
            : string.Join(", ", edges.Select(static edge => $"{edge.Source}->{edge.Target}").Order(StringComparer.Ordinal));
}

internal sealed record OracleBaseline(string Solution, int OracleEdges, int CorrectToday, int FalsePositivesToday);

internal readonly record struct ProjectEdge(string Source, string Target);

internal sealed record SolutionScore(
    string Solution,
    int OracleEdges,
    int Correct,
    IReadOnlyCollection<ProjectEdge> FalsePositiveEdges,
    IReadOnlyCollection<ProjectEdge> MissingEdges)
{
    public int FalsePositives => FalsePositiveEdges.Count;
}

/// <summary>
/// Analyses the whole ArchitectureDependencyLab corpus once and decodes the Project-scope
/// <c>ProjectReference</c> edges out of the committed package. The fixture is versioned, so this never
/// skips: an absent corpus is a failure, not a skip.
/// <para>
/// The wire form is decoded here rather than read through <c>Csharp2Md.Core</c>, whose local-handle and
/// enum types are internal: the expectation must not be derived from the code being measured.
/// </para>
/// </summary>
public sealed class ArchitectureDependencyLabPackage : IAsyncLifetime
{
    internal static readonly string Root = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "ArchitectureDependencyLab");

    internal static readonly ImmutableArray<string> SolutionPaths =
    [
        Path.Combine(Root, "src", "SistemaA", "SistemaA.slnx"),
        Path.Combine(Root, "src", "SistemaB", "SistemaB.slnx"),
        Path.Combine(Root, "src", "SistemaC", "SistemaC.slnx"),
        Path.Combine(Root, "src", "SistemaD", "SistemaD.slnx"),
        Path.Combine(Root, "src", "SistemaE", "SistemaE.slnx"),
        Path.Combine(Root, "src", "SistemaB", "Copias", "SistemaE.Copia", "SistemaE.Copia.slnx"),
    ];

    // The wire form writes the aggregation scope and the dependency category as enum ordinals:
    // Document, Project, Component, DeploymentUnit and ProjectReference, InternalInvocation,
    // StructuralTypeUse, Http, Grpc, Messaging, Contract, Persistence.
    private const int ProjectScope = 1;
    private const int ProjectReferenceCategory = 0;

    private readonly string output = CliTestPaths.UniqueOutputPath();
    private Dictionary<string, HashSet<ProjectEdge>> stated = [];

    public async Task InitializeAsync()
    {
        string[] arguments =
        [
            "analyze",
            .. SolutionPaths.SelectMany(static solution => new[] { "--solution", solution }),
            "--output",
            output,
        ];

        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(arguments);

        Assert.True(exitCode == ExitCodes.Success, $"analyze exited {exitCode}: {stderr}");
        Assert.Contains("committed and certified", stdout, StringComparison.Ordinal);
        stated = ReadProjectReferenceEdges(output);
    }

    public Task DisposeAsync()
    {
        CliTestPaths.TryDeleteDirectory(output);
        return Task.CompletedTask;
    }

    internal IReadOnlyCollection<ProjectEdge> StatedEdges(string solution) =>
        stated.TryGetValue(solution, out var edges) ? edges : [];

    internal SolutionScore Score(string solution)
    {
        var expected = ReadOracle()[solution];
        var produced = StatedEdges(solution);

        return new SolutionScore(
            solution,
            expected.Count,
            produced.Count(expected.Contains),
            produced.Where(edge => !expected.Contains(edge)).ToArray(),
            expected.Where(edge => !produced.Contains(edge)).ToArray());
    }

    internal static IReadOnlyDictionary<string, IReadOnlySet<ProjectEdge>> ReadOracle()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(Root, "oracle", "project-references.json")));

        return document.RootElement.EnumerateArray()
            .GroupBy(static reference => reference.GetProperty("solution").GetString()!, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlySet<ProjectEdge>)group
                    .Select(static reference => new ProjectEdge(
                        reference.GetProperty("sourceProject").GetString()!,
                        reference.GetProperty("targetProject").GetString()!))
                    .ToHashSet(),
                StringComparer.Ordinal);
    }

    private static Dictionary<string, HashSet<ProjectEdge>> ReadProjectReferenceEdges(string package)
    {
        using var pointer = JsonDocument.Parse(File.ReadAllText(Path.Combine(package, "manifest.json")));
        var solutions = Path.Combine(
            package,
            "generations",
            pointer.RootElement.GetProperty("generation").GetString()!,
            "solutions");

        var edgesBySolution = new Dictionary<string, HashSet<ProjectEdge>>(StringComparer.Ordinal);
        foreach (var solution in Directory.EnumerateDirectories(solutions))
        {
            var entities = ReadShards(Path.Combine(solution, "tables"), "entities.*.json", static shard => shard
                .RootElement.EnumerateArray()
                .Select(static entity => entity.GetString()!));

            var dependencies = ReadShards(Path.Combine(solution, "measures"), "dependencies.*.json", static shard => shard
                .RootElement.GetProperty("dependencies")
                .EnumerateArray()
                .Where(static dependency =>
                    dependency.GetProperty("scope").GetInt32() == ProjectScope
                    && dependency.GetProperty("category").GetInt32() == ProjectReferenceCategory)
                .Select(static dependency => (
                    Source: dependency.GetProperty("source").GetString()!,
                    Target: dependency.GetProperty("target").GetString()!)));

            foreach (var (source, target) in dependencies)
            {
                var (solutionName, sourceProject) = ParseProjectKey(entities[Ordinal(source)]);
                var (_, targetProject) = ParseProjectKey(entities[Ordinal(target)]);

                if (!edgesBySolution.TryGetValue(solutionName, out var edges))
                {
                    edges = [];
                    edgesBySolution.Add(solutionName, edges);
                }

                edges.Add(new ProjectEdge(sourceProject, targetProject));
            }
        }

        return edgesBySolution;
    }

    private static List<T> ReadShards<T>(string directory, string pattern, Func<JsonDocument, IEnumerable<T>> read)
    {
        var items = new List<T>();
        foreach (var shard in Directory.EnumerateFiles(directory, pattern).Order(StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(shard));
            items.AddRange(read(document));
        }

        return items;
    }

    /// <summary>
    /// An entity key reads <c>project:solution:&lt;solution&gt;:&lt;solution file&gt;:&lt;project path&gt;</c>;
    /// the oracle names projects bare, so the file name without its extension is what is compared.
    /// </summary>
    private static (string Solution, string Project) ParseProjectKey(string key)
    {
        var segments = key.Split(':', 5);
        Assert.True(segments.Length == 5 && segments[0] == "project", $"'{key}' is not a project entity key.");

        return (segments[2], Path.GetFileNameWithoutExtension(segments[4].Replace('\\', '/')));
    }

    /// <summary>Local handles are lowercase base36 ordinals into the solution's sorted entity table.</summary>
    private static int Ordinal(string handle)
    {
        const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyz";

        var ordinal = 0;
        foreach (var character in handle)
        {
            var digit = Alphabet.IndexOf(character, StringComparison.Ordinal);
            Assert.True(digit >= 0, $"'{handle}' is not a base36 local handle.");
            ordinal = (ordinal * 36) + digit;
        }

        return ordinal;
    }
}
