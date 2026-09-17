using Csharp2Md.Core.Analysis.Semantics;

namespace Csharp2Md.Core.Tests.Analysis;

public sealed class ProjectVariantPlannerTests
{
    [Fact]
    [Trait("Requirement", "VAR-01")]
    [Trait("Requirement", "VAR-02")]
    [Trait("Requirement", "CRT-06")]
    public async Task DiscoverAsync_AcmePayments_EmitsEvaluatedProjectTfmPairsWithoutGlobalTargetFramework()
    {
        var solutionPath = Path.Combine(
            CoreTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Payments",
            "Acme.Payments.slnx");
        var root = Path.Combine(CoreTestPaths.RepoRoot, "fixtures", "SyntheticSolution");

        var plan = await ProjectVariantPlanner.DiscoverAsync(solutionPath, root);

        Assert.Contains(plan, variant =>
            variant.ProjectLogicalRelativePath.EndsWith("Acme.Payments/Acme.Payments.csproj", StringComparison.Ordinal)
            && variant.TargetFramework == "net10.0");
        Assert.Contains(plan, variant =>
            variant.ProjectLogicalRelativePath.EndsWith("Acme.Shared.Contracts/Acme.Shared.Contracts.csproj", StringComparison.Ordinal)
            && variant.TargetFramework == "net10.0");
        Assert.Equal(plan.Length, plan.Distinct().Count());
        Assert.All(plan, variant => Assert.DoesNotContain('\\', variant.ProjectLogicalRelativePath));
        Assert.All(plan, variant => Assert.False(string.IsNullOrWhiteSpace(variant.TargetFramework)));
    }

    [Fact]
    [Trait("Requirement", "VAR-02")]
    public async Task DiscoverAsync_AcmeOrdersWithBrokenSdkProject_FailsAsVariantPlan()
    {
        var solutionPath = Path.Combine(
            CoreTestPaths.RepoRoot,
            "fixtures",
            "SyntheticSolution",
            "Acme.Orders",
            "Acme.Orders.slnx");
        var root = Path.Combine(CoreTestPaths.RepoRoot, "fixtures", "SyntheticSolution");

        var exception = await Assert.ThrowsAsync<VariantPlanException>(
            () => ProjectVariantPlanner.DiscoverAsync(solutionPath, root));

        Assert.Equal("variant-plan", exception.Code);
        Assert.Contains("Acme.Broken", exception.Cause, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "VAR-01")]
    public async Task DiscoverAsync_MultiTargetTempProject_DeduplicatesPerSolutionAndKeepsBothTfms()
    {
        using var tree = new TempSolutionTree();
        var solutionPath = tree.WriteMultiTargetSolution();

        var plan = await ProjectVariantPlanner.DiscoverAsync(solutionPath, tree.Root);

        var library = Assert.Single(
            plan.GroupBy(variant => variant.ProjectLogicalRelativePath),
            group => group.Key.EndsWith("Library.csproj", StringComparison.Ordinal));
        Assert.Equal(
            new[] { "net10.0", "net8.0" },
            library.Select(variant => variant.TargetFramework).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    [Trait("Requirement", "VAR-02")]
    public void BuildPlan_MissingTfmForSdkProject_FailsAsVariantPlan()
    {
        using var tree = new TempSolutionTree();
        var project = tree.WriteFile("src/Library/Library.csproj", "<Project />");

        var exception = Assert.Throws<VariantPlanException>(
            () => ProjectVariantPlanner.BuildPlan(
                tree.Root,
                [new VariantResolveReport(project, null)]));

        Assert.Equal("variant-plan", exception.Code);
        Assert.StartsWith("missing-tfm:", exception.Cause, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "VAR-02")]
    public void BuildPlan_EmptyFilePathWithTfm_FailsAsUnmatched()
    {
        var exception = Assert.Throws<VariantPlanException>(
            () => ProjectVariantPlanner.BuildPlan(
                Path.GetTempPath(),
                [new VariantResolveReport("", "net10.0")]));

        Assert.Equal("variant-plan", exception.Code);
        Assert.Equal("unmatched-tfm-progress", exception.Cause);
    }

    [Fact]
    [Trait("Requirement", "VAR-02")]
    public void BuildPlan_AmbiguousEmptyAndPresentTfm_FailsWithoutGuessing()
    {
        using var tree = new TempSolutionTree();
        var project = tree.WriteFile("src/Library/Library.csproj", "<Project />");

        var exception = Assert.Throws<VariantPlanException>(
            () => ProjectVariantPlanner.BuildPlan(
                tree.Root,
                [
                    new VariantResolveReport(project, "net10.0"),
                    new VariantResolveReport(project, null),
                ]));

        Assert.Equal("variant-plan", exception.Code);
        Assert.StartsWith("ambiguous-tfm:", exception.Cause, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "VAR-01")]
    public void BuildPlan_DeduplicatesIdenticalProjectTfmPairs()
    {
        using var tree = new TempSolutionTree();
        var project = tree.WriteFile("src/Library/Library.csproj", "<Project />");

        var plan = ProjectVariantPlanner.BuildPlan(
            tree.Root,
            [
                new VariantResolveReport(project, "net10.0"),
                new VariantResolveReport(project, "net10.0"),
                new VariantResolveReport(project, "net8.0"),
            ]);

        Assert.Equal(2, plan.Length);
        Assert.Equal(
            new[] { "net10.0", "net8.0" }.Order(StringComparer.Ordinal),
            plan.Select(variant => variant.TargetFramework).Order(StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "CRT-06")]
    public void CoreProject_DoesNotReferenceMicrosoftBuildOrRegisterMsBuildLocator()
    {
        var csprojPath = Path.Combine(CoreTestPaths.RepoRoot, "src", "Csharp2Md.Core", "Csharp2Md.Core.csproj");
        var csproj = File.ReadAllText(csprojPath);
        Assert.DoesNotContain("Microsoft.Build.", csproj, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Microsoft.CodeAnalysis.Workspaces.MSBuild", csproj, StringComparison.Ordinal);

        var sourceFiles = Directory.EnumerateFiles(
            Path.Combine(CoreTestPaths.RepoRoot, "src", "Csharp2Md.Core"),
            "*.cs",
            SearchOption.AllDirectories);
        Assert.All(sourceFiles, path =>
        {
            var text = File.ReadAllText(path);
            Assert.DoesNotContain("MSBuildLocator.RegisterDefaults", text, StringComparison.Ordinal);
            Assert.DoesNotContain("using Microsoft.Build", text, StringComparison.Ordinal);
        });
    }

    [Fact]
    [Trait("Requirement", "VAR-02")]
    public void BuildPlan_NoResolveReports_FailsAsMissingProgress()
    {
        var exception = Assert.Throws<VariantPlanException>(
            () => ProjectVariantPlanner.BuildPlan(Path.GetTempPath(), []));

        Assert.Equal("variant-plan", exception.Code);
        Assert.Equal("missing-tfm-progress", exception.Cause);
    }

    [Fact]
    [Trait("Requirement", "PKG-07")]
    public void BuildPlan_ProjectOutsideAuthorizedRoot_IsRejected()
    {
        using var tree = new TempSolutionTree();
        var outside = tree.WriteOutsideFile("escape/Library.csproj", "<Project />");

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProjectVariantPlanner.BuildPlan(
                tree.Root,
                [new VariantResolveReport(outside, "net10.0")]));

        Assert.Contains("escapes", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TempSolutionTree : IDisposable
    {
        private readonly string _basePath;

        public TempSolutionTree()
        {
            _basePath = Path.Combine(Path.GetTempPath(), "csharp2md-variant-plan-" + Guid.NewGuid().ToString("N"));
            Root = Path.Combine(_basePath, "root");
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string WriteFile(string relativePath, string contents)
        {
            var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
            return fullPath;
        }

        public string WriteOutsideFile(string relativePath, string contents)
        {
            var fullPath = Path.Combine(_basePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, contents);
            return fullPath;
        }

        public string WriteMultiTargetSolution()
        {
            WriteFile(
                "src/Library/Library.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                </Project>
                """);
            WriteFile("src/Library/Class1.cs", "namespace Library; public class Class1;");
            var solutionPath = WriteFile(
                "Library.slnx",
                """
                <Solution>
                  <Project Path="src/Library/Library.csproj" />
                </Solution>
                """);
            return solutionPath;
        }

        public void Dispose() => TempPath.TryDelete(_basePath);
    }
}
