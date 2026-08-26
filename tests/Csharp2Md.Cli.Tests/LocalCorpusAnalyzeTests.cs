namespace Csharp2Md.Cli.Tests;

public sealed class LocalCorpusAnalyzeTests
{
    public static TheoryData<string, string> LocalCorpora { get; } = new()
    {
        { "eShop", Path.Combine("fixtures", "eShop", "eShop.slnx") },
        { "eShopOnContainers", Path.Combine("fixtures", "eShopOnContainers", "eShopOnContainers-ServicesAndWebApps.sln") },
    };

    [Theory]
    [MemberData(nameof(LocalCorpora))]
    [Trait("Category", "LocalCorpus")]
    public async Task Analyze_LocalCorpus_WritesPackageWhenCloneIsPresent(string name, string relativeSolution)
    {
        var solutionPath = Path.Combine(CliTestPaths.RepoRoot, relativeSolution);
        if (!File.Exists(solutionPath))
        {
            throw new InvalidOperationException(
                string.Concat("$XunitDynamicSkip$", $"local {name} clone is not present at '{solutionPath}'."));
        }

        var outputPath = CliTestPaths.UniqueOutputPath();
        try
        {
            var (exitCode, _, _) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath]);

            Assert.Equal(0, exitCode);
            var manifest = Assert.Single(
                Directory.EnumerateFiles(outputPath, "manifest.json", SearchOption.AllDirectories));
            Assert.Equal("manifest.json", Path.GetFileName(manifest));
            Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(manifest), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
        }
    }
}
