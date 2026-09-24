using System.Text.Json;

namespace Csharp2Md.Cli.Tests;

/// <summary>
/// Acceptance for the optional local clones. Every case carries <c>Category=LocalCorpus</c>, which the
/// mandatory gates exclude with <c>--filter "Category!=LocalCorpus"</c>, and a clone that is absent is
/// reported as a named skip rather than a failure -- no mandatory gate depends on a clone existing.
/// </summary>
public sealed class LocalCorpusAnalyzeTests
{
    private const long MiB = 1024L * 1024L;

    [LocalCorpusFact(LocalCorpus.EShop, LocalCorpus.EShopSolution)]
    [Trait("Category", "LocalCorpus")]
    public async Task Analyze_eShop_CompletesWithoutCrossProjectVariantCollision()
    {
        var package = CliTestPaths.UniqueOutputPath();
        try
        {
            var stderr = await AnalyzeAsync(LocalCorpus.EShop, LocalCorpus.EShopSolution, package);

            Assert.DoesNotContain("variant-collision", stderr, StringComparison.Ordinal);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(package);
        }
    }

    [LocalCorpusFact(LocalCorpus.EShop, LocalCorpus.EShopSolution)]
    [Trait("Category", "LocalCorpus")]
    [Trait("Requirement", "DEP-01")]
    public async Task Analyze_eShop_ComponentScopeHasNoCrossComponentFalsePositive()
    {
        // DEP-01 regression: Component-scope lifting once crossed every relation's source component
        // against the full membership of a shared symbol's *target* entity - a shared library type used
        // by every host fanned out to 138 of 144 possible component pairs on this exact corpus (measured
        // 2026-09-17). The fix requires a relation's own endpoints to be attributable to both components,
        // not merely "some occurrence of each entity exists somewhere in that component" - so a real,
        // multi-service solution like eShop must show zero false cross-component pairs.
        const int ComponentScope = 2;
        var package = CliTestPaths.UniqueOutputPath();
        try
        {
            await AnalyzeAsync(LocalCorpus.EShop, LocalCorpus.EShopSolution, package);

            using var pointer = JsonDocument.Parse(File.ReadAllText(Path.Combine(package, "manifest.json")));
            var solutionDir = Directory.EnumerateDirectories(Path.Combine(
                package,
                "generations",
                pointer.RootElement.GetProperty("generation").GetString()!,
                "solutions")).Single();

            var entities = ReadShards(Path.Combine(solutionDir, "tables"), "entities.*.json", static shard => shard
                .RootElement.EnumerateArray()
                .Select(static entity => entity.GetString()!)).ToArray();

            var componentPairs = ReadShards(Path.Combine(solutionDir, "measures"), "dependencies.*.json", static shard => shard
                    .RootElement.GetProperty("dependencies")
                    .EnumerateArray()
                    .Where(static dependency => dependency.GetProperty("scope").GetInt32() == ComponentScope)
                    .Select(static dependency => (
                        Source: dependency.GetProperty("source").GetString()!,
                        Target: dependency.GetProperty("target").GetString()!)))
                .Select(pair => (Source: entities[Ordinal(pair.Source)], Target: entities[Ordinal(pair.Target)]))
                .Distinct()
                .ToArray();

            var crossComponent = componentPairs.Where(pair => pair.Source != pair.Target).ToArray();

            Assert.True(
                crossComponent.Length == 0,
                "Component-scope aggregated a relation between two distinct components without the relation's "
                + "own endpoints proving it - found: " + string.Join(", ", crossComponent.Select(static p => $"{p.Source}->{p.Target}")));
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(package);
        }
    }

    [LocalCorpusFact(LocalCorpus.EShopOnContainers, LocalCorpus.EShopOnContainersSolution)]
    [Trait("Category", "LocalCorpus")]
    public async Task Analyze_eShopOnContainers_CommitsWithinFileAndByteCeilings()
    {
        var package = CliTestPaths.UniqueOutputPath();
        try
        {
            await AnalyzeAsync(LocalCorpus.EShopOnContainers, LocalCorpus.EShopOnContainersSolution, package);

            AssertCommittedCeiling(package, maximumFiles: 1_500, maximumBytes: 64 * MiB);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(package);
        }
    }

    [LocalCorpusFact(LocalCorpus.Pitstop, LocalCorpus.PitstopSolution)]
    [Trait("Category", "LocalCorpus")]
    public async Task Analyze_Pitstop_CommitsWithinFileAndByteCeilings()
    {
        var package = CliTestPaths.UniqueOutputPath();
        try
        {
            await AnalyzeAsync(LocalCorpus.Pitstop, LocalCorpus.PitstopSolution, package);

            AssertCommittedCeiling(package, maximumFiles: 750, maximumBytes: 25 * MiB);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(package);
        }
    }

    [Fact]
    [Trait("Category", "LocalCorpus")]
    public void AbsentClone_SkipsWithAReasonNamingTheCloneAndItsPath()
    {
        var absent = new LocalCorpusFactAttribute("eShop", "absent-clone.slnx");

        Assert.Equal(
            $"local eShop clone is not present at '{Path.Combine(CliTestPaths.RepoRoot, "fixtures", "eShop", "absent-clone.slnx")}'.",
            absent.Skip);
    }

    [Fact]
    [Trait("Category", "LocalCorpus")]
    public void PresentClone_RunsInsteadOfSkipping() =>
        Assert.Null(new LocalCorpusFactAttribute("..", "csharp2md.slnx").Skip);

    [Fact]
    [Trait("Category", "LocalCorpus")]
    public void LocalCorpora_AreGitIgnoredAndAnalyzedOutsideTheRepository()
    {
        var ignored = File.ReadAllLines(Path.Combine(CliTestPaths.RepoRoot, ".gitignore"))
            .Select(static line => line.Trim())
            .ToArray();

        Assert.Contains($"fixtures/{LocalCorpus.EShop}/", ignored);
        Assert.Contains($"fixtures/{LocalCorpus.EShopOnContainers}/", ignored);
        Assert.Contains($"fixtures/{LocalCorpus.Pitstop}/", ignored);
        Assert.DoesNotContain(
            CliTestPaths.RepoRoot,
            CliTestPaths.UniqueOutputPath(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> AnalyzeAsync(string clone, string solutionFileName, string package)
    {
        var (exitCode, stdout, stderr) = await CliInvoke.RunAsync(
            ["analyze", "--solution", LocalCorpus.SolutionPath(clone, solutionFileName), "--output", package]);

        Assert.True(exitCode == ExitCodes.Success, $"analyze exited {exitCode}: {stderr}");
        Assert.Contains("committed and certified", stdout, StringComparison.Ordinal);
        return stderr;
    }

    private static void AssertCommittedCeiling(string package, int maximumFiles, long maximumBytes)
    {
        var rootManifest = Path.Combine(package, "manifest.json");
        using var pointer = JsonDocument.Parse(File.ReadAllText(rootManifest));
        var committed = Path.Combine(
            package,
            "generations",
            pointer.RootElement.GetProperty("generation").GetString()!);

        var reachable = Directory.EnumerateFiles(committed, "*", SearchOption.AllDirectories)
            .Append(rootManifest)
            .Select(static path => new FileInfo(path).Length)
            .ToArray();

        Assert.InRange(reachable.Length, 1, maximumFiles);
        Assert.InRange(reachable.Sum(), 1L, maximumBytes);
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

public static class LocalCorpus
{
    public const string EShop = "eShop";
    public const string EShopSolution = "eShop.slnx";
    public const string EShopOnContainers = "eShopOnContainers";
    public const string EShopOnContainersSolution = "eShopOnContainers-ServicesAndWebApps.sln";
    public const string Pitstop = "Pitstop";
    public const string PitstopSolution = "pitstop.sln";

    public static string SolutionPath(string clone, string solutionFileName) =>
        Path.Combine(CliTestPaths.RepoRoot, "fixtures", clone, solutionFileName);
}

/// <summary>
/// Marks a case that needs an optional local clone. The clone is resolved at discovery: when its
/// solution file is absent the case reports a skip naming the clone and the path it looked for, so a
/// machine without the clone never fails the run. xunit 2.9.3 does not honour the v3
/// <c>$XunitDynamicSkip$</c> token, so the decision is made here rather than thrown from the body.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalCorpusFactAttribute : FactAttribute
{
    public LocalCorpusFactAttribute(string clone, string solutionFileName)
    {
        var solutionPath = LocalCorpus.SolutionPath(clone, solutionFileName);
        if (!File.Exists(solutionPath))
        {
            Skip = $"local {clone} clone is not present at '{solutionPath}'.";
        }
    }
}
