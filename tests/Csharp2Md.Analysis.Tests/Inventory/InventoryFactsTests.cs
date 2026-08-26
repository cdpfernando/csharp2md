using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class InventoryFactsTests
{
    [Fact]
    [Trait("Requirement", "ROSE-09")]
    [Trait("Requirement", "ROSE-10")]
    [Trait("Requirement", "ROSE-13")]
    public void Create_AcmeOrders_SolutionIdUsesDefaultWorkspaceAndFilename()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));

        var facts = InventoryFacts.Create(solutionPath, listed, root);

        var expected = Solution.Create(
            SolutionId.Create(WorkspaceIdentity.Create("default"), "Acme.Orders.slnx"));
        Assert.Equal(expected.Id, facts.Solution.Id);
        Assert.Contains("default", facts.Solution.Id.Value, StringComparison.Ordinal);
        Assert.Contains("Acme.Orders.slnx", facts.Solution.Id.Value, StringComparison.Ordinal);
        Assert.Equal(expected, facts.Solution);
    }

    [Fact]
    [Trait("Requirement", "ROSE-03")]
    public void Create_AcmeSharedContracts_LogicalPathIsRelativeToSyntheticSolution()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
        AssertEqualPaths(
            Path.Combine(AnalysisTestPaths.RepoRoot, "fixtures", "SyntheticSolution"),
            root);

        var facts = InventoryFacts.Create(solutionPath, listed, root);

        const string expectedPath = "Acme.Shared.Contracts/Acme.Shared.Contracts.csproj";
        Assert.DoesNotContain("..", expectedPath, StringComparison.Ordinal);
        var expectedId = ProjectId.Create(facts.Solution.Id, expectedPath);
        Assert.Contains(facts.Projects, project => project.Id.Equals(expectedId));
        Assert.DoesNotContain(facts.Projects, project => project.Id.Value.Contains("..", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "ROSE-03")]
    public void Create_MissingListedPath_IsNotEmittedAsAProjectFact()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));

        var facts = InventoryFacts.Create(solutionPath, listed, root);

        Assert.Equal(4, facts.Projects.Length);
        Assert.Contains(facts.Projects, project => project.Id.Equals(ProjectId.Create(facts.Solution.Id, "Acme.Orders/Acme.Orders.csproj")));
        Assert.Contains(facts.Projects, project => project.Id.Equals(ProjectId.Create(facts.Solution.Id, "Acme.Broken/Acme.Broken.csproj")));
        Assert.Contains(facts.Projects, project => project.Id.Equals(ProjectId.Create(facts.Solution.Id, "Acme.Orders.Worker/Acme.Orders.Worker.csproj")));
        Assert.DoesNotContain(
            facts.Projects,
            project => project.Id.Value.Contains("Acme.DoesNotExist", StringComparison.Ordinal));
    }

    private static string AcmeOrdersSolutionPath()
    {
        var path = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(path), $"Expected fixture at '{path}'.");
        return path;
    }

    private static IReadOnlyList<string> ExistingAbsolutePaths(string solutionPath, IEnumerable<string> listed)
    {
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))
            ?? throw new InvalidOperationException($"'{solutionPath}' has no directory.");
        return listed
            .Select(path => Path.GetFullPath(Path.Combine(solutionDirectory, path)))
            .Where(File.Exists)
            .ToArray();
    }

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
