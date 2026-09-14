using System.CommandLine;
using System.CommandLine.Help;
using Csharp2Md.Cli;
using System.Text.Json;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeOptionSurfaceTests
{
    private static readonly string[] RemovedOptionNames =
    [
        "--topic",
        "--domain",
        "--manifest",
        "--trust",
        "--include-source-generators",
        "--analysis-timeout",
    ];

    [Fact]
    [Trait("Requirement", "ENG-45")]
    [Trait("Requirement", "STOR-47")]
    [Trait("Requirement", "STOR-52")]
    [Trait("Requirement", "ROSE-61")]
    [Trait("Requirement", "APR-08")]
    public void AnalyzeAndRoot_DoNotExposeRemovedMarkdownEraOptions()
    {
        var root = CommandFactory.CreateRootCommand();
        var analyze = Assert.Single(root.Subcommands, static command => command.Name == "analyze");

        var rootNames = OptionNames(root.Options);
        var analyzeNames = OptionNames(analyze.Options);

        foreach (var removed in RemovedOptionNames)
        {
            Assert.DoesNotContain(removed, rootNames);
            Assert.DoesNotContain(removed, analyzeNames);
        }

        var analyzeProductOptions = analyze.Options
            .Where(static option => option is not HelpOption and not VersionOption)
            .Select(static option => option.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["--allowlist", "--max-file-reads-per-scenario", "--output", "--reading-budget-tokens", "--solution"],
            analyzeProductOptions);
        Assert.Empty(analyze.Arguments);
    }

    [Fact]
    [Trait("Requirement", "ENG-45")]
    [Trait("Requirement", "STOR-47")]
    [Trait("Requirement", "STOR-52")]
    [Trait("Requirement", "ROSE-61")]
    public void LaunchSettings_UsesAnalyzeSolutionAgainstTheFixture()
    {
        var path = Path.Combine(
            CliTestPaths.RepoRoot,
            "src",
            "Csharp2Md.Cli",
            "Properties",
            "launchSettings.json");
        Assert.True(File.Exists(path), $"launchSettings.json was not found at '{path}'.");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var profiles = document.RootElement.GetProperty("profiles");
        Assert.True(profiles.EnumerateObject().Any(), "launchSettings.json must declare at least one profile.");

        foreach (var profile in profiles.EnumerateObject())
        {
            var args = profile.Value.GetProperty("commandLineArgs").GetString();
            Assert.False(string.IsNullOrWhiteSpace(args), $"Profile '{profile.Name}' has empty commandLineArgs.");
            Assert.StartsWith("analyze --solution ", args, StringComparison.Ordinal);
            Assert.Contains("--output artifacts/analyze-out", args, StringComparison.Ordinal);

            foreach (var removed in RemovedOptionNames)
            {
                Assert.DoesNotContain(removed, args, StringComparison.Ordinal);
            }
        }
    }

    private static IReadOnlyList<string> OptionNames(IEnumerable<Option> options) =>
        options
            .SelectMany(static option => option.Aliases.Append(option.Name))
            .ToArray();
}
