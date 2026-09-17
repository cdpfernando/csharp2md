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
