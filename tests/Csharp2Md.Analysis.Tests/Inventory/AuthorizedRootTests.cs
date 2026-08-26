using Csharp2Md.Analysis.Inventory;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class AuthorizedRootTests
{
    [Fact]
    [Trait("Requirement", "ROSE-01")]
    public void Compute_AcmeOrdersPlusExistingSiblings_IsSyntheticSolutionRoot()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var existingProjects = ExistingAcmeOrdersProjects();

        var root = AuthorizedRoot.Compute(solutionPath, existingProjects);

        AssertEqualPaths(SyntheticSolutionRoot(), root);
    }

    [Fact]
    [Trait("Requirement", "ROSE-01")]
    public void Compute_MissingListedPath_DoesNotChangeTheRoot()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var existingProjects = ExistingAcmeOrdersProjects();
        var missing = Path.Combine(SyntheticSolutionRoot(), "Acme.DoesNotExist", "Acme.DoesNotExist.csproj");
        Assert.False(File.Exists(missing), $"Precondition failed: '{missing}' unexpectedly exists.");

        var withoutMissing = AuthorizedRoot.Compute(solutionPath, existingProjects);
        var withMissing = AuthorizedRoot.Compute(solutionPath, [.. existingProjects, missing]);

        AssertEqualPaths(SyntheticSolutionRoot(), withoutMissing);
        AssertEqualPaths(withoutMissing, withMissing);
    }

    [Fact]
    [Trait("Requirement", "ROSE-01")]
    public void Compute_ProjectsAllUnderTheSolutionDirectory_KeepsThatDirectory()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-authorized-root-");
        try
        {
            var appDirectory = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(appDirectory);
            var solutionPath = Path.Combine(appDirectory, "App.slnx");
            var projectPath = Path.Combine(appDirectory, "App.csproj");
            File.WriteAllText(solutionPath, "<Solution />");
            File.WriteAllText(projectPath, "<Project />");

            var root = AuthorizedRoot.Compute(solutionPath, [projectPath]);

            AssertEqualPaths(appDirectory, root);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(SyntheticSolutionRoot(), "Acme.Orders", "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static string[] ExistingAcmeOrdersProjects()
    {
        string[] relative =
        [
            Path.Combine("Acme.Orders", "Acme.Orders.csproj"),
            Path.Combine("Acme.Shared.Contracts", "Acme.Shared.Contracts.csproj"),
            Path.Combine("Acme.Broken", "Acme.Broken.csproj"),
        ];

        return relative.Select(item =>
        {
            var path = Path.Combine(SyntheticSolutionRoot(), item);
            Assert.True(File.Exists(path), $"Expected existing project at '{path}'.");
            return path;
        }).ToArray();
    }

    private static string SyntheticSolutionRoot() =>
        Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution");

    private static void AssertEqualPaths(string expected, string actual)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        Assert.Equal(Normalize(expected), Normalize(actual), StringComparer.FromComparison(comparison));
    }

    private static string Normalize(string path)
    {
        var full = Path.GetFullPath(path);
        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
