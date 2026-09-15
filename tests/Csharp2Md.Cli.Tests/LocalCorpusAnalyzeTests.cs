using Csharp2Md.Cli;

namespace Csharp2Md.Cli.Tests;

public sealed class LocalCorpusAnalyzeTests
{
    public static TheoryData<string, string> LocalCorpora { get; } = new()
    {
        { "eShop", Path.Combine("fixtures", "eShop", "eShop.slnx") },
        { "eShopOnContainers", Path.Combine("fixtures", "eShopOnContainers", "eShopOnContainers-ServicesAndWebApps.sln") },
    };

    private static readonly string[] PitstopIsolatedProjects =
    [
        "Infrastructure.Messaging",
        "CustomerManagementAPI",
        "VehicleManagementAPI",
        "WorkshopManagementAPI",
        "WorkshopManagement.UnitTests",
        "WorkshopManagementEventHandler",
        "AuditlogService",
        "InvoiceService",
        "NotificationService",
        "TimeService",
        "WebApp",
        "TestUtils",
        "UITest",
        "InvoiceService.UnitTests",
        "NotificationService.UnitTests",
    ];

    public static TheoryData<string, string> PitstopCorpora { get; } = CreatePitstopCorpora();

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

    [Theory]
    [MemberData(nameof(PitstopCorpora))]
    [Trait("Category", "LocalCorpus")]
    [Trait("Requirement", "APR-41")]
    [Trait("Requirement", "APR-42")]
    public async Task Analyze_Pitstop_WritesPackageWhenCloneIsPresent(string name, string relativePath)
    {
        var sourcePath = Path.Combine(CliTestPaths.RepoRoot, relativePath);
        if (!File.Exists(sourcePath))
        {
            throw new InvalidOperationException(
                string.Concat("$XunitDynamicSkip$", $"local {name} clone is not present at '{sourcePath}'."));
        }

        var outputPath = CliTestPaths.UniqueOutputPath();
        string? isolatedSolution = null;
        try
        {
            var solutionPath = MaterializeSolutionIfNeeded(sourcePath, out isolatedSolution);
            var (exitCode, _, stderr) = await CliInvoke.RunAsync(
                ["analyze", "--solution", solutionPath, "--output", outputPath]);

            Assert.True(
                exitCode is ExitCodes.Success or ExitCodes.Degraded or ExitCodes.CertificationFailed,
                $"Pitstop analyze did not write a package; exit {exitCode}: {stderr}");
            var manifest = Assert.Single(
                Directory.EnumerateFiles(outputPath, "manifest.json", SearchOption.AllDirectories));
            Assert.Equal("manifest.json", Path.GetFileName(manifest));
            Assert.StartsWith(Path.GetFullPath(outputPath), Path.GetFullPath(manifest), StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CliTestPaths.TryDeleteDirectory(outputPath);
            if (isolatedSolution is not null && File.Exists(isolatedSolution))
            {
                File.Delete(isolatedSolution);
            }
        }
    }

    private static TheoryData<string, string> CreatePitstopCorpora()
    {
        var data = new TheoryData<string, string>
        {
            { "Pitstop", Path.Combine("fixtures", "Pitstop", "pitstop.sln") },
        };

        foreach (var project in PitstopIsolatedProjects)
        {
            data.Add(
                "Pitstop." + project,
                Path.Combine("fixtures", "Pitstop", project, project + ".csproj"));
        }

        return data;
    }

    private static string MaterializeSolutionIfNeeded(string sourcePath, out string? isolatedSolution)
    {
        if (sourcePath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            || sourcePath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            isolatedSolution = null;
            return sourcePath;
        }

        var directory = Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException($"Project path '{sourcePath}' has no directory.");
        isolatedSolution = Path.Combine(
            directory,
            Path.GetFileNameWithoutExtension(sourcePath) + ".csharp2md-isolated.slnx");
        var projectFileName = Path.GetFileName(sourcePath);
        File.WriteAllText(
            isolatedSolution,
            $"<Solution>{Environment.NewLine}  <Project Path=\"{projectFileName}\" />{Environment.NewLine}</Solution>{Environment.NewLine}");
        return isolatedSolution;
    }
}
