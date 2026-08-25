using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class DocumentInventoryTests
{
    [Fact]
    [Trait("Requirement", "ROSE-04")]
    public void Collect_OrdersController_RelativePathUsesForwardSlashesAndHasNoDrivePrefix()
    {
        var solutionPath = AcmeOrdersSolutionPath();
        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
        var facts = InventoryFacts.Create(solutionPath, listed, root);
        var ordersId = ProjectId.Create(facts.Solution.Id, "Acme.Orders/Acme.Orders.csproj");
        var orders = Assert.Single(facts.Projects, project => project.Id.Equals(ordersId));
        var projectPath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(solutionPath) ?? throw new InvalidOperationException(solutionPath),
            "Acme.Orders.csproj"));

        var documents = DocumentInventory.Collect(root, orders, projectPath);

        var controller = Assert.Single(
            documents,
            document => string.Equals(document.RelativePath, "Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal));
        Assert.Equal("Acme.Orders/Api/OrdersController.cs", controller.RelativePath);
        Assert.DoesNotContain('\\', controller.RelativePath);
        Assert.False(Path.IsPathRooted(controller.RelativePath));
        Assert.False(HasDrivePrefix(controller.RelativePath));
        Assert.Equal(Document.Create(orders.Id, "Acme.Orders/Api/OrdersController.cs"), controller);
    }

    [Fact]
    [Trait("Requirement", "ROSE-04")]
    [Trait("Requirement", "ROSE-53")]
    public void Collect_PlantedBuildOutputUnderProjectDirectory_IsNotInventoried()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-doc-inv-bin-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(Path.Combine(projectDir, "bin"));
            Directory.CreateDirectory(Path.Combine(projectDir, "obj"));
            Directory.CreateDirectory(Path.Combine(projectDir, ".git"));
            Directory.CreateDirectory(Path.Combine(projectDir, ".vs"));
            File.WriteAllText(
                Path.Combine(projectDir, "App.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDir, "Program.cs"), "class Program;");
            File.WriteAllText(Path.Combine(projectDir, "bin", "Generated.cs"), "class Generated;");
            File.WriteAllText(Path.Combine(projectDir, "obj", "Generated.cs"), "class ObjGenerated;");
            File.WriteAllText(Path.Combine(projectDir, ".git", "hook.cs"), "class GitHook;");
            File.WriteAllText(Path.Combine(projectDir, ".vs", "cache.cs"), "class VsCache;");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");

            var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
            var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
            var facts = InventoryFacts.Create(solutionPath, listed, root);
            var project = Assert.Single(facts.Projects);

            var documents = DocumentInventory.Collect(root, project, Path.Combine(projectDir, "App.csproj"));

            Assert.Contains(documents, document => string.Equals(document.RelativePath, "Program.cs", StringComparison.Ordinal));
            Assert.DoesNotContain(
                documents,
                document => document.RelativePath.Contains("Generated.cs", StringComparison.Ordinal)
                    || document.RelativePath.Contains("hook.cs", StringComparison.Ordinal)
                    || document.RelativePath.Contains("cache.cs", StringComparison.Ordinal)
                    || document.RelativePath.Split('/').Any(static segment =>
                        segment is "bin" or "obj" or ".git" or ".vs"));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
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

    private static bool HasDrivePrefix(string path) =>
        path.Length >= 2 && char.IsAsciiLetter(path[0]) && path[1] == ':';
}
