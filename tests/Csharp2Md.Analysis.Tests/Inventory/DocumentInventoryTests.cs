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

        var documents = DocumentInventory.Collect(root, orders, projectPath).Documents;

        var controller = Assert.Single(
            documents,
            document => string.Equals(document.RelativePath, "Acme.Orders/Api/OrdersController.cs", StringComparison.Ordinal));
        Assert.Equal("Acme.Orders/Api/OrdersController.cs", controller.RelativePath);
        Assert.DoesNotContain('\\', controller.RelativePath);
        Assert.False(Path.IsPathRooted(controller.RelativePath));
        Assert.False(HasDrivePrefix(controller.RelativePath));
        AssertInventoried(controller, orders.Id, "Acme.Orders/Api/OrdersController.cs");
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

            var documents = DocumentInventory.Collect(root, project, Path.Combine(projectDir, "App.csproj")).Documents;

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

    [Fact]
    [Trait("Requirement", "ROSE-05")]
    [Trait("Requirement", "ROSE-06")]
    public void Collect_PlantedJsonUnderProjectDirectory_IsDocumentPlusUnsupportedDiagnostic()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-doc-inv-json-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
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
            File.WriteAllText(Path.Combine(projectDir, "notes.json"), """{"ok":true}""");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");

            var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
            var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
            var facts = InventoryFacts.Create(solutionPath, listed, root);
            var project = Assert.Single(facts.Projects);

            var inventoried = DocumentInventory.Collect(root, project, Path.Combine(projectDir, "App.csproj"));

            var json = Assert.Single(
                inventoried.Documents,
                document => string.Equals(document.RelativePath, "notes.json", StringComparison.Ordinal));
            AssertInventoried(json, project.Id, "notes.json");
            var unsupported = Assert.Single(
                inventoried.Diagnostics,
                record => string.Equals(record.IdentityOrKey, "notes.json", StringComparison.Ordinal));
            Assert.Equal("unsupported-document", unsupported.Code);
            Assert.False(Path.IsPathRooted(unsupported.IdentityOrKey));
            Assert.DoesNotContain(
                inventoried.CSharpDocuments,
                document => string.Equals(document.RelativePath, "notes.json", StringComparison.Ordinal));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-05")]
    [Trait("Requirement", "ROSE-06")]
    public void Collect_CSharpDocuments_DoNotReceiveUnsupportedDocument()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-doc-inv-cs-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
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
            File.WriteAllText(Path.Combine(projectDir, "notes.json"), """{"ok":true}""");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");

            var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
            var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
            var facts = InventoryFacts.Create(solutionPath, listed, root);
            var project = Assert.Single(facts.Projects);

            var inventoried = DocumentInventory.Collect(root, project, Path.Combine(projectDir, "App.csproj"));

            Assert.Contains(
                inventoried.CSharpDocuments,
                document => string.Equals(document.RelativePath, "Program.cs", StringComparison.Ordinal));
            Assert.DoesNotContain(
                inventoried.Diagnostics,
                record => string.Equals(record.IdentityOrKey, "Program.cs", StringComparison.Ordinal)
                    || (record.IdentityOrKey is not null
                        && record.IdentityOrKey.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)));
            Assert.All(
                inventoried.Diagnostics,
                record => Assert.Equal("unsupported-document", record.Code));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "ROSE-04")]
    public void Collect_ParentProjectAtSolutionRoot_DoesNotInventoryNestedProjectDocuments()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-doc-inv-nested-");
        try
        {
            var rootDir = tree.FullName;
            var nestedDir = Path.Combine(rootDir, "Nested", "App");
            Directory.CreateDirectory(nestedDir);
            var composePath = Path.Combine(rootDir, "docker-compose.dcproj");
            var nestedProjectPath = Path.Combine(nestedDir, "App.csproj");
            File.WriteAllText(
                composePath,
                """
                <Project Sdk="Microsoft.Docker.Sdk">
                  <ItemGroup>
                    <None Include="docker-compose.yml" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(rootDir, "docker-compose.yml"), "services: {}");
            File.WriteAllText(
                nestedProjectPath,
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(nestedDir, "Program.cs"), "class Program;");
            var solutionPath = Path.Combine(rootDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="docker-compose.dcproj" /><Project Path="Nested/App/App.csproj" /></Solution>""");

            var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
            var existing = ExistingAbsolutePaths(solutionPath, listed);
            var root = AuthorizedRoot.Compute(solutionPath, existing);
            var facts = InventoryFacts.Create(solutionPath, listed, root);
            var compose = Assert.Single(
                facts.Projects,
                project => project.Id.Value.Contains("docker-compose.dcproj", StringComparison.Ordinal));
            var nested = Assert.Single(
                facts.Projects,
                project => project.Id.Value.Contains("App.csproj", StringComparison.Ordinal));

            var composeDocuments = DocumentInventory.Collect(root, compose, composePath, existing).Documents;
            var nestedDocuments = DocumentInventory.Collect(root, nested, nestedProjectPath, existing).Documents;

            Assert.DoesNotContain(
                composeDocuments,
                document => document.RelativePath.EndsWith("Program.cs", StringComparison.Ordinal));
            Assert.Contains(
                composeDocuments,
                document => string.Equals(document.RelativePath, "docker-compose.yml", StringComparison.Ordinal));
            var program = Assert.Single(
                nestedDocuments,
                document => document.RelativePath.EndsWith("Program.cs", StringComparison.Ordinal));
            Assert.Equal(nested.Id, program.OwningProject);
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-25")]
    public void Collect_MatchingAppsettingsNames_AreConfigurationDocumentsWithoutUnsupportedDiagnostic()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-doc-inv-appsettings-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
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
            File.WriteAllText(Path.Combine(projectDir, "appsettings.json"), """{"Services":{"PaymentService":"https://payments.example"}}""");
            File.WriteAllText(Path.Combine(projectDir, "appsettings.Development.json"), """{"Services":{"ShippingService":"https://shipping.example"}}""");
            File.WriteAllText(Path.Combine(projectDir, "AppSettings.Production.json"), """{"Services":{"PaymentService":"https://payments.prod.example"}}""");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");

            var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
            var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
            var facts = InventoryFacts.Create(solutionPath, listed, root);
            var project = Assert.Single(facts.Projects);

            var inventoried = DocumentInventory.Collect(root, project, Path.Combine(projectDir, "App.csproj"));

            AssertConfigurationDocument(inventoried, project, "appsettings.json");
            AssertConfigurationDocument(inventoried, project, "appsettings.Development.json");
            AssertConfigurationDocument(inventoried, project, "AppSettings.Production.json");
            AssertUnsupportedDocument(inventoried, "App.csproj");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-25")]
    public void Collect_NearMissMyAppsettingsJson_IsUnsupportedNotConfiguration()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-doc-inv-myappsettings-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
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
            File.WriteAllText(Path.Combine(projectDir, "myappsettings.json"), """{"ok":true}""");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");

            var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
            var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
            var facts = InventoryFacts.Create(solutionPath, listed, root);
            var project = Assert.Single(facts.Projects);

            var inventoried = DocumentInventory.Collect(root, project, Path.Combine(projectDir, "App.csproj"));

            var nearMiss = Assert.Single(
                inventoried.Documents,
                document => string.Equals(document.RelativePath, "myappsettings.json", StringComparison.Ordinal));
            AssertInventoried(nearMiss, project.Id, "myappsettings.json");
            Assert.DoesNotContain(
                inventoried.ConfigurationDocuments,
                document => string.Equals(document.RelativePath, "myappsettings.json", StringComparison.Ordinal));
            AssertUnsupportedDocument(inventoried, "myappsettings.json");
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    [Fact]
    [Trait("Requirement", "CDC-25")]
    public void Collect_UnmatchedNonCsharpIncludingCsproj_KeepsUnsupportedDocument()
    {
        var tree = Directory.CreateTempSubdirectory("csharp2md-doc-inv-unmatched-");
        try
        {
            var projectDir = Path.Combine(tree.FullName, "App");
            Directory.CreateDirectory(projectDir);
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
            File.WriteAllText(Path.Combine(projectDir, "notes.json"), """{"ok":true}""");
            var solutionPath = Path.Combine(projectDir, "App.slnx");
            File.WriteAllText(solutionPath, """<Solution><Project Path="App.csproj" /></Solution>""");

            var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
            var root = AuthorizedRoot.Compute(solutionPath, ExistingAbsolutePaths(solutionPath, listed));
            var facts = InventoryFacts.Create(solutionPath, listed, root);
            var project = Assert.Single(facts.Projects);

            var inventoried = DocumentInventory.Collect(root, project, Path.Combine(projectDir, "App.csproj"));

            AssertUnsupportedDocument(inventoried, "notes.json");
            AssertUnsupportedDocument(inventoried, "App.csproj");
            Assert.Empty(inventoried.ConfigurationDocuments);
            Assert.DoesNotContain(
                inventoried.Diagnostics,
                record => string.Equals(record.IdentityOrKey, "Program.cs", StringComparison.Ordinal));
        }
        finally
        {
            tree.Delete(recursive: true);
        }
    }

    private static void AssertConfigurationDocument(
        InventoriedDocuments inventoried,
        Project project,
        string relativePath)
    {
        var document = Assert.Single(
            inventoried.Documents,
            candidate => string.Equals(candidate.RelativePath, relativePath, StringComparison.Ordinal));
        AssertInventoried(document, project.Id, relativePath);
        Assert.Contains(
            inventoried.ConfigurationDocuments,
            candidate => candidate.Equals(document));
        Assert.DoesNotContain(
            inventoried.CSharpDocuments,
            candidate => string.Equals(candidate.RelativePath, relativePath, StringComparison.Ordinal));
        Assert.DoesNotContain(
            inventoried.Diagnostics,
            record => string.Equals(record.IdentityOrKey, relativePath, StringComparison.Ordinal));
    }

    private static void AssertUnsupportedDocument(InventoriedDocuments inventoried, string relativePath)
    {
        Assert.Contains(
            inventoried.Documents,
            document => string.Equals(document.RelativePath, relativePath, StringComparison.Ordinal));
        Assert.DoesNotContain(
            inventoried.ConfigurationDocuments,
            document => string.Equals(document.RelativePath, relativePath, StringComparison.Ordinal));
        var unsupported = Assert.Single(
            inventoried.Diagnostics,
            record => string.Equals(record.IdentityOrKey, relativePath, StringComparison.Ordinal));
        Assert.Equal("unsupported-document", unsupported.Code);
    }

    private static void AssertInventoried(Document document, ProjectId projectId, string relativePath)
    {
        Assert.Equal(Document.Create(projectId, relativePath).Reference, document.Reference);
        Assert.NotNull(document.SourceHash);
        Assert.Equal(relativePath, document.RelativePath);
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
