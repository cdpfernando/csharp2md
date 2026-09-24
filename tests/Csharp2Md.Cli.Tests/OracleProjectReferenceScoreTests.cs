using System.Text.Json;

namespace Csharp2Md.Cli.Tests;

/// <summary>
/// The dependency ratchet. It analyses the six solutions of <c>fixtures/ArchitectureDependencyLab</c>
/// in one package and scores the Project-scope <c>ProjectReference</c> edges the generator publishes
/// against that corpus's oracle, per solution.
/// <para>
/// The analysis runs without <c>--include-tests</c> and PKG-05 keeps test projects out of the default
/// package, so part of the oracle is unreachable by construction and scoring against all of it would
/// charge the generator for edges it is told to exclude. Of the 87 <c>ProjectReference</c> edges the
/// oracle names, 27 involve a <c>.Testes</c> project; <b>the 60 that remain are the target</b>. Both 87
/// and the 27 excluded stay in every failure message so the full picture is visible.
/// </para>
/// <para>
/// Every stated edge lands in exactly one of three buckets, counted apart and never folded together.
/// <i>Correct</i> matches a reachable oracle edge. <i>False positive</i> matches no oracle edge at all.
/// <i>Test-policy leak</i> matches an oracle edge that involves a <c>.Testes</c> project: a real edge, so
/// not a dependency error, but evidence of the PKG-05 test-leakage defect this project already recorded
/// in <c>.specs/STATE.md</c>.
/// </para>
/// <para>
/// <b>Phase 11 (T73-T76, 2026-09-17) took the generator from 7 correct of 60 reachable edges / 29 false
/// positives / 4 test-policy leaks to a fully correct projection: 60 correct, 0 false positives, 0
/// leaks.</b> T73 fixed <c>ProjectVariantWorkspace.ReferencedProjects()</c> to read the solution's real
/// <c>Project.ProjectReferences</c> and two aggregation bugs in <c>CausalRelationExtractor</c> (an
/// evidence key that collided across roots referencing the same target, and a referenced project's
/// occurrence attributed to the citing root instead of to itself). T75/T76 then excluded test-project
/// entities and their outbound edges from retention by default and taught the test-naming heuristic the
/// corpus's own <c>.Testes</c> (Portuguese) convention, closing the remaining leaks to zero.
/// <c>fixtures/SyntheticSolution</c> could not have caught any of this: with two entities a complete graph
/// and a correct graph are the same graph.
/// </para>
/// <para>
/// Each case is a three-sided ratchet, kept even at the ceiling: fewer correct edges, more false
/// positives or more test-policy leaks fail as a regression, and so - now vacuously, since 60/60 with no
/// false positive or leak is the ceiling - would stating even more than the truth. The failure message
/// always carries the corpus target (60 correct of 87, 27 excluded, no false positive and no leak) so a
/// future regression's distance from a correct generator is printed, not inferred.
/// </para>
/// </summary>
public sealed class OracleProjectReferenceScoreTests : IClassFixture<ArchitectureDependencyLabPackage>
{
    private readonly ArchitectureDependencyLabPackage package;

    public OracleProjectReferenceScoreTests(ArchitectureDependencyLabPackage package) => this.package = package;

    /// <summary>
    /// Re-measured on 2026-09-17 after Phase 11 (T73-T76), against the vendored corpus. <c>OracleEdges</c>
    /// and <c>TestPolicyEdges</c> state a truth and are read back from the oracle so the corpus cannot
    /// drift under the ratchet; <c>CorrectToday</c>, <c>FalsePositivesToday</c> and
    /// <c>TestPolicyLeaksToday</c> now pin the correct state rather than a defect: every reachable edge in
    /// every solution matches the oracle exactly, no false positive remains, and no edge sourced from a
    /// <c>.Testes</c> project leaks into the default package.
    /// </summary>
    internal static readonly ImmutableArray<OracleBaseline> RecordedBaselines =
    [
        new("SistemaA", OracleEdges: 4, TestPolicyEdges: 1, CorrectToday: 3, FalsePositivesToday: 0, TestPolicyLeaksToday: 0),
        new("SistemaB", OracleEdges: 18, TestPolicyEdges: 6, CorrectToday: 12, FalsePositivesToday: 0, TestPolicyLeaksToday: 0),
        // SistemaC has no reachable edge at all: its one oracle edge is excluded by PKG-05. Its case cannot
        // score anything, only detect a false positive or a leak; the other five carry the scoring duty.
        new("SistemaC", OracleEdges: 1, TestPolicyEdges: 1, CorrectToday: 0, FalsePositivesToday: 0, TestPolicyLeaksToday: 0),
        new("SistemaD", OracleEdges: 8, TestPolicyEdges: 3, CorrectToday: 5, FalsePositivesToday: 0, TestPolicyLeaksToday: 0),
        new("SistemaE", OracleEdges: 28, TestPolicyEdges: 8, CorrectToday: 20, FalsePositivesToday: 0, TestPolicyLeaksToday: 0),
        new("SistemaE.Copia", OracleEdges: 28, TestPolicyEdges: 8, CorrectToday: 20, FalsePositivesToday: 0, TestPolicyLeaksToday: 0),
    ];

    public static TheoryData<string> Solutions =>
        new(RecordedBaselines.Select(static defect => defect.Solution));

    [Theory]
    [MemberData(nameof(Solutions))]
    [Trait("Requirement", "DEP-01")]
    [Trait("Category", "OracleCorpus")]
    public void ProjectReferences_HoldTheRecordedDefectBaseline(string solution)
    {
        var baseline = RecordedBaselines.Single(defect => defect.Solution == solution);
        var score = package.Score(solution);

        Assert.Equal(baseline.OracleEdges, score.OracleEdges);
        Assert.Equal(baseline.TestPolicyEdges, score.TestPolicyEdges);
        Assert.True(score.Correct >= baseline.CorrectToday, Verdict(score, baseline));
        Assert.True(score.FalsePositives <= baseline.FalsePositivesToday, Verdict(score, baseline));
        Assert.True(score.TestPolicyLeaks <= baseline.TestPolicyLeaksToday, Verdict(score, baseline));
        Assert.True(
            score.Correct == baseline.CorrectToday
            && score.FalsePositives == baseline.FalsePositivesToday
            && score.TestPolicyLeaks == baseline.TestPolicyLeaksToday,
            Verdict(score, baseline));
    }

    /// <summary>
    /// SistemaC's only oracle edge is <c>SistemaC.Testes -&gt; SistemaC.ApiMonolitica</c>, which PKG-05 keeps
    /// out of the default package, so in default mode SistemaC has nothing to score. Saying that plainly is
    /// honest where a 0-of-1 score would read as a generator failure rather than a policy exclusion. Its
    /// false positives and test-policy leaks still ratchet in
    /// <see cref="ProjectReferences_HoldTheRecordedDefectBaseline"/>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "DEP-01")]
    [Trait("Category", "OracleCorpus")]
    public void SistemaC_HasNoScoreableEdgeInDefaultMode()
    {
        var score = package.Score("SistemaC");

        Assert.Equal(1, score.OracleEdges);
        Assert.Equal("SistemaC.Testes->SistemaC.ApiMonolitica", Format(score.ExcludedEdges));
        Assert.True(
            score.ReachableEdges == 0 && score.Correct == 0 && score.MissingEdges.Count == 0,
            $"SistemaC is expected to have nothing scoreable in default mode, yet it reports "
            + $"{score.ReachableEdges} reachable oracle edges, {score.Correct} correct and "
            + $"{Format(score.MissingEdges)} missing. Its single oracle edge is excluded by PKG-05; if that "
            + "changed, the corpus or the exclusion rule moved and this case must be re-derived.");
    }

    [Fact]
    [Trait("Requirement", "DEP-01")]
    [Trait("Category", "OracleCorpus")]
    public void Corpus_ScoresTheRecordedShareOfItsReachableOracle()
    {
        var scores = RecordedBaselines.Select(defect => package.Score(defect.Solution)).ToArray();
        var correct = scores.Sum(static score => score.Correct);
        var falsePositives = scores.Sum(static score => score.FalsePositives);
        var leaks = scores.Sum(static score => score.TestPolicyLeaks);
        var oracleEdges = scores.Sum(static score => score.OracleEdges);
        var excluded = scores.Sum(static score => score.TestPolicyEdges);
        var reachable = scores.Sum(static score => score.ReachableEdges);

        Assert.Equal(OracleCorpusEdges, oracleEdges);
        Assert.Equal(TestPolicyExcludedEdges, excluded);
        Assert.Equal(ReachableCorpusEdges, reachable);
        Assert.True(
            correct == BaselineCorrect
            && falsePositives == BaselineFalsePositives
            && leaks == BaselineTestPolicyLeaks,
            $"The corpus baseline moved: {correct} correct, {falsePositives} false positives and {leaks} "
            + $"test-policy leaks against a recorded {BaselineCorrect}, {BaselineFalsePositives} and "
            + $"{BaselineTestPolicyLeaks}. A correct generator states {reachable} of {reachable} reachable "
            + $"edges with no false positive and no leak; the oracle names {oracleEdges} in all, of which "
            + $"{excluded} involve a test project PKG-05 excludes from the default package. Raise or lower "
            + "the baselines in this file in the same commit that moved them.");
    }

    /// <summary>
    /// PKG-05 excludes "usos de tipo nao retidos": before T74, a use of or call to a BCL/framework symbol
    /// (<c>string</c>, <c>int</c>, <c>Task</c>, ...) was retained as a shared <c>Symbol</c>/<c>Callable</c>
    /// entity whose occurrence spans every project that happens to use it, producing a near-complete
    /// Component-scope dependency graph (measured on a real corpus in <c>.specs/STATE.md</c>: 52 identical
    /// outgoing edges per component page). This corpus's business classes use plenty of BCL types
    /// (<c>string</c>, collections, <c>Task</c>) yet retains none of them as entities.
    /// </summary>
    [Fact]
    [Trait("Requirement", "PKG-05")]
    [Trait("Category", "OracleCorpus")]
    public void Entities_NeverRetainABareSymbolOrCallableFromOutsideTheAnalyzedSource()
    {
        foreach (var defect in RecordedBaselines)
        {
            var leaked = package.EntityKeys(defect.Solution)
                .Where(static key => key.StartsWith("symbol:", StringComparison.Ordinal) || key.StartsWith("callable:", StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                leaked.Length == 0,
                $"{defect.Solution} retains {leaked.Length} symbol/callable entity(ies) from outside its own "
                + $"source: {string.Join(", ", leaked)}. PKG-05 excludes non-retained type uses; a BCL or "
                + "framework symbol has no declaration in the analyzed solution and must not survive retention.");
        }
    }

    /// <summary>
    /// T75/T76/T77: no entity whose logical path names a <c>.Testes</c> project - a retained Document,
    /// Component or DeploymentUnit, or a document/project reached only as a dependency's Document/Project-
    /// scope target through membership lifting - survives in the default package. `default mode` here is
    /// exactly this fixture's own analysis, run without <c>--include-tests</c>.
    /// </summary>
    [Fact]
    [Trait("Requirement", "PKG-05")]
    [Trait("Category", "OracleCorpus")]
    public void Entities_NeverNameATestesProjectByDefault()
    {
        foreach (var defect in RecordedBaselines)
        {
            var leaked = package.EntityKeys(defect.Solution)
                .Where(static key => key.Contains(".Testes", StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                leaked.Length == 0,
                $"{defect.Solution} retains {leaked.Length} entity(ies) naming a .Testes project by default: "
                + $"{string.Join(", ", leaked)}. PKG-05 excludes tests from the default package.");
        }
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

        foreach (var defect in RecordedBaselines)
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
    internal const int TestPolicyExcludedEdges = 27;
    internal const int ReachableCorpusEdges = OracleCorpusEdges - TestPolicyExcludedEdges;
    private const int BaselineCorrect = 60;
    private const int BaselineFalsePositives = 0;
    private const int BaselineTestPolicyLeaks = 0;

    private static string Verdict(SolutionScore score, OracleBaseline baseline) =>
        $"{score.Solution}: {score.Correct} correct (baseline {baseline.CorrectToday}), "
        + $"{score.FalsePositives} false positives (baseline {baseline.FalsePositivesToday}) and "
        + $"{score.TestPolicyLeaks} test-policy leaks (baseline {baseline.TestPolicyLeaksToday}) against "
        + $"{score.ReachableEdges} reachable oracle edges -- {score.OracleEdges} in all, of which "
        + $"{score.TestPolicyEdges} name a test project PKG-05 excludes from the default package. The corpus "
        + $"target is {ReachableCorpusEdges} correct of {OracleCorpusEdges} oracle edges "
        + $"({TestPolicyExcludedEdges} excluded), with no false positive and no leak. Fewer correct edges, "
        + "more false positives or more leaks is a regression; an improvement means this baseline is stale "
        + "and must be raised in the commit that improved it. False positives now: "
        + $"{Format(score.FalsePositiveEdges)}. Test-policy leaks now: {Format(score.TestPolicyLeakEdges)}. "
        + $"Missing now: {Format(score.MissingEdges)}.";

    private static string Format(IReadOnlyCollection<ProjectEdge> edges) =>
        edges.Count == 0
            ? "none"
            : string.Join(", ", edges.Select(static edge => $"{edge.Source}->{edge.Target}").Order(StringComparer.Ordinal));
}

internal sealed record OracleBaseline(
    string Solution,
    int OracleEdges,
    int TestPolicyEdges,
    int CorrectToday,
    int FalsePositivesToday,
    int TestPolicyLeaksToday);

internal readonly record struct ProjectEdge(string Source, string Target);

/// <summary>
/// Every stated edge lands in exactly one bucket: <see cref="CorrectEdges"/> matches a reachable oracle
/// edge, <see cref="FalsePositiveEdges"/> matches no oracle edge at all, and <see cref="TestPolicyLeakEdges"/>
/// matches an oracle edge PKG-05 keeps out of the default package. <see cref="ExcludedEdges"/> is that
/// unreachable part of the oracle, and <see cref="MissingEdges"/> only ever names reachable edges.
/// </summary>
internal sealed record SolutionScore(
    string Solution,
    int OracleEdges,
    IReadOnlyCollection<ProjectEdge> ExcludedEdges,
    IReadOnlyCollection<ProjectEdge> CorrectEdges,
    IReadOnlyCollection<ProjectEdge> FalsePositiveEdges,
    IReadOnlyCollection<ProjectEdge> TestPolicyLeakEdges,
    IReadOnlyCollection<ProjectEdge> MissingEdges)
{
    public int TestPolicyEdges => ExcludedEdges.Count;

    public int ReachableEdges => OracleEdges - ExcludedEdges.Count;

    public int Correct => CorrectEdges.Count;

    public int FalsePositives => FalsePositiveEdges.Count;

    public int TestPolicyLeaks => TestPolicyLeakEdges.Count;
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
    private Dictionary<string, List<string>> entitiesBySolution = [];

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
        (stated, entitiesBySolution) = ReadProjectReferenceEdges(output);
    }

    public Task DisposeAsync()
    {
        CliTestPaths.TryDeleteDirectory(output);
        return Task.CompletedTask;
    }

    internal IReadOnlyCollection<ProjectEdge> StatedEdges(string solution) =>
        stated.TryGetValue(solution, out var edges) ? edges : [];

    internal IReadOnlyList<string> EntityKeys(string solution) =>
        entitiesBySolution.TryGetValue(solution, out var keys) ? keys : [];

    internal SolutionScore Score(string solution)
    {
        var expected = ReadOracle()[solution];
        var excluded = expected.Where(IsExcludedByTestPolicy).ToHashSet();
        var reachable = expected.Where(edge => !excluded.Contains(edge)).ToHashSet();
        var produced = StatedEdges(solution);

        return new SolutionScore(
            solution,
            expected.Count,
            excluded.ToArray(),
            produced.Where(reachable.Contains).ToArray(),
            produced.Where(edge => !expected.Contains(edge)).ToArray(),
            produced.Where(excluded.Contains).ToArray(),
            reachable.Where(edge => !produced.Contains(edge)).ToArray());
    }

    /// <summary>
    /// PKG-05 keeps test projects out of the default package and the analysis runs without
    /// <c>--include-tests</c>, so an oracle edge naming one is unreachable by construction rather than
    /// missed. The corpus names them in Portuguese: <c>&lt;system&gt;.Testes</c>.
    /// </summary>
    internal static bool IsExcludedByTestPolicy(ProjectEdge edge) =>
        IsTestProject(edge.Source) || IsTestProject(edge.Target);

    private static bool IsTestProject(string project) =>
        project.EndsWith(".Testes", StringComparison.Ordinal);

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

    private static (Dictionary<string, HashSet<ProjectEdge>> Edges, Dictionary<string, List<string>> Entities) ReadProjectReferenceEdges(string package)
    {
        using var pointer = JsonDocument.Parse(File.ReadAllText(Path.Combine(package, "manifest.json")));
        var solutions = Path.Combine(
            package,
            "generations",
            pointer.RootElement.GetProperty("generation").GetString()!,
            "solutions");

        var edgesBySolution = new Dictionary<string, HashSet<ProjectEdge>>(StringComparer.Ordinal);
        var entitiesBySolution = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var solution in Directory.EnumerateDirectories(solutions))
        {
            var entities = ReadShards(Path.Combine(solution, "tables"), "entities.*.json", static shard => shard
                .RootElement.EnumerateArray()
                .Select(static entity => entity.GetString()!));

            // Every entity key reads <kind>:solution:<name>:<solution file>:<rest>, so any entity in this
            // solution's own shard names it - a solution with zero entities cannot occur since it always
            // has at least its own Solution/Project entities.
            if (entities.Count > 0)
            {
                entitiesBySolution[SolutionNameOf(entities[0])] = entities;
            }

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

        return (edgesBySolution, entitiesBySolution);
    }

    /// <summary>Every entity key reads <c>&lt;kind&gt;:solution:&lt;name&gt;:&lt;solution file&gt;:&lt;rest&gt;</c>.</summary>
    private static string SolutionNameOf(string entityKey) => entityKey.Split(':', 5)[2];

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
