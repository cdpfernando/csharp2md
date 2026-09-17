namespace Csharp2Md.Cli.Tests.Isolation;

public sealed class FixtureRetentionTests
{
    [Fact]
    [Trait("Requirement", "CRT-08")]
    public void SyntheticSolution_ExistsWithASolutionOrProjectFile()
    {
        var path = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "SyntheticSolution");
        Assert.True(Directory.Exists(path), $"fixtures/SyntheticSolution was not found at '{path}'.");

        var projectOrSolutionFiles = Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            .Where(file =>
                file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
                || file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(file => Path.GetRelativePath(path, file).Replace('\\', '/'))
            .ToArray();

        Assert.True(
            projectOrSolutionFiles.Length > 0,
            $"fixtures/SyntheticSolution at '{path}' contains no .slnx or .csproj file.");
    }

    [Fact]
    [Trait("Requirement", "CRT-08")]
    public void Gitignore_ExcludesLocalEShopCorpora()
    {
        var gitignorePath = Path.Combine(CliTestPaths.RepoRoot, ".gitignore");
        Assert.True(File.Exists(gitignorePath), $".gitignore was not found at '{gitignorePath}'.");

        var gitignore = File.ReadAllText(gitignorePath);
        Assert.Contains("fixtures/eShop/", gitignore, StringComparison.Ordinal);
        Assert.Contains("fixtures/eShopOnContainers/", gitignore, StringComparison.Ordinal);
        Assert.Contains("fixtures/Pitstop/", gitignore, StringComparison.Ordinal);
    }
}
