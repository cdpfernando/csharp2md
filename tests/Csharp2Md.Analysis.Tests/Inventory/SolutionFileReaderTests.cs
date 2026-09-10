using Csharp2Md.Analysis.Inventory;

namespace Csharp2Md.Analysis.Tests.Inventory;

public sealed class SolutionFileReaderTests
{
    [Fact]
    [Trait("Requirement", "ROSE-03")]
    public void ReadProjectPaths_AcmeOrdersSlnx_ReturnsListedRelativePaths()
    {
        var solutionPath = Path.Combine(
            AnalysisTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        Assert.True(File.Exists(solutionPath), $"Expected fixture at '{solutionPath}'.");

        var paths = SolutionFileReader.ReadProjectPaths(solutionPath).ToArray();

        Assert.Equal(
            [
                "Acme.Orders.csproj",
                "../Acme.Shared.Contracts/Acme.Shared.Contracts.csproj",
                "../Acme.Broken/Acme.Broken.csproj",
                "../Acme.DoesNotExist/Acme.DoesNotExist.csproj",
                "../Acme.Orders.Worker/Acme.Orders.Worker.csproj",
            ],
            paths);
        Assert.Contains(paths, path => path.Contains("Acme.Orders", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.Contains("Acme.Shared.Contracts", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.Contains("Acme.Broken", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.Contains("Acme.DoesNotExist", StringComparison.Ordinal));
        Assert.Contains(paths, path => path.Contains("Acme.Orders.Worker", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "ROSE-03")]
    public void ReadProjectPaths_ClassicSlnProjectLine_YieldsTheListedPath()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-solution-reader-");
        try
        {
            const string listedPath = @"src\Acme.Orders\Acme.Orders.csproj";
            var solutionPath = Path.Combine(tree.FullName, "Acme.Orders.sln");
            File.WriteAllText(
                solutionPath,
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Acme.Orders", "src\Acme.Orders\Acme.Orders.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Global
                EndGlobal
                """);

            var paths = SolutionFileReader.ReadProjectPaths(solutionPath);

            Assert.Equal(listedPath, Assert.Single(paths));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-103")]
    public void ReadProjectPaths_ClassicSlnWithSolutionFolder_ExcludesTheFolderButYieldsTheProject()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-solution-reader-folder-");
        try
        {
            const string listedPath = @"src\Acme.Orders\Acme.Orders.csproj";
            var solutionPath = Path.Combine(tree.FullName, "Acme.Orders.sln");
            File.WriteAllText(
                solutionPath,
                """
                Microsoft Visual Studio Solution File, Format Version 12.00
                Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "Solution Items", "Solution Items", "{22222222-2222-2222-2222-222222222222}"
                EndProject
                Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Acme.Orders", "src\Acme.Orders\Acme.Orders.csproj", "{11111111-1111-1111-1111-111111111111}"
                EndProject
                Global
                EndGlobal
                """);

            var paths = SolutionFileReader.ReadProjectPaths(solutionPath);

            Assert.Equal(listedPath, Assert.Single(paths));
            Assert.DoesNotContain("Solution Items", paths);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "GCPC-103")]
    public void ReadProjectPaths_SlnxWithFolderElement_FolderWasNeverAffectedAndProjectIsListed()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-solution-reader-slnx-folder-");
        try
        {
            var solutionPath = Path.Combine(tree.FullName, "Acme.Orders.slnx");
            File.WriteAllText(
                solutionPath,
                """
                <Solution>
                  <Folder Name="/Docs/">
                    <File Path="README.md" />
                  </Folder>
                  <Project Path="Acme.Orders.csproj" />
                </Solution>
                """);

            var paths = SolutionFileReader.ReadProjectPaths(solutionPath);

            Assert.Equal("Acme.Orders.csproj", Assert.Single(paths));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }
}
