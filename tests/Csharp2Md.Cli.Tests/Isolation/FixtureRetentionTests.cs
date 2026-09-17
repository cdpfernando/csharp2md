using System.Text.Json;

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
    public void ArchitectureDependencyLab_ShipsItsSixSolutionsOracleAndLocalFeed()
    {
        var path = Path.Combine(CliTestPaths.RepoRoot, "fixtures", "ArchitectureDependencyLab");

        Assert.Equal(6, Directory.EnumerateFiles(path, "*.slnx", SearchOption.AllDirectories).Count());
        Assert.Equal(41, Directory.EnumerateFiles(path, "*.csproj", SearchOption.AllDirectories).Count());

        // The oracle is what dependency claims are scored against, so its size is pinned here: a corpus
        // that silently loses references would otherwise make the generator look better than it is.
        using var oracle = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(path, "oracle", "project-references.json")));
        Assert.Equal(87, oracle.RootElement.GetArrayLength());

        // Vendoring the two private packages keeps the fixture self-contained: analysing it needs no
        // `dotnet pack` of the corpus's own package-source, and no restore at all.
        Assert.Equal(2, Directory.EnumerateFiles(Path.Combine(path, "local-feed"), "*.nupkg").Count());
    }

    [Fact]
    [Trait("Requirement", "CRT-08")]
    public void Gitignore_KeepsTheLabsBuildOutputOutAndItsPrivateFeedIn()
    {
        var gitignore = File.ReadAllLines(Path.Combine(CliTestPaths.RepoRoot, ".gitignore"))
            .Select(static line => line.Trim())
            .ToArray();

        // The Roslyn BuildHost writes bin/obj inside the lab whenever it is analysed; that output must
        // never become committable, and the vendored nupkgs must survive the blanket *.nupkg rule.
        Assert.Contains("[Bb]in/", gitignore);
        Assert.Contains("[Oo]bj/", gitignore);
        Assert.Contains("!fixtures/ArchitectureDependencyLab/local-feed/*.nupkg", gitignore);
    }

    [Fact]
    [Trait("Requirement", "CRT-08")]
    public void Solution_LeavesTheAnalysisFixturesOutOfTheBuild()
    {
        var solution = File.ReadAllText(Path.Combine(CliTestPaths.RepoRoot, "csharp2md.slnx"));

        Assert.DoesNotContain("fixtures", solution, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// PKG-10 keeps only the current contract in the repository. Two committed <c>analyze</c> outputs used
    /// to sit under <c>fixtures/</c> carrying the superseded percent-encoded <c>id1:</c> identity scheme;
    /// they are gone, and the ignore rule keeps the next stray run from replacing them.
    /// </summary>
    [Fact]
    [Trait("Requirement", "PKG-10")]
    public void Fixtures_CarryNoStrayAnalyzeOutput()
    {
        var fixtures = Path.Combine(CliTestPaths.RepoRoot, "fixtures");
        var strays = Directory.EnumerateDirectories(fixtures, "csharp2md-analyze-out-*")
            .Select(directory => Path.GetRelativePath(CliTestPaths.RepoRoot, directory).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            strays.Length == 0,
            $"fixtures/ holds leftover analyze output: {string.Join(", ", strays)}. PKG-10 keeps only the "
            + "current contract in the repository; delete the directory rather than committing it.");
        Assert.Contains(
            "fixtures/csharp2md-analyze-out-*/",
            File.ReadAllText(Path.Combine(CliTestPaths.RepoRoot, ".gitignore")),
            StringComparison.Ordinal);
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
