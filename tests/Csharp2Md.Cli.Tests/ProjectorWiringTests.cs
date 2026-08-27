using System.CommandLine;
using System.CommandLine.Help;
using Csharp2Md.Cli;

namespace Csharp2Md.Cli.Tests;

public sealed class ProjectorWiringTests
{
    [Fact]
    [Trait("Requirement", "RP-06")]
    public void CommandFactory_ConstructsStoreWithPackageProjector()
    {
        var source = File.ReadAllText(
            Path.Combine(CliTestPaths.RepoRoot, "src", "Csharp2Md.Cli", "CommandFactory.cs"));

        Assert.Contains("new FilesystemTransactionalStore(outputPath, new PackageProjector())", source, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "RP-06")]
    public void Analyze_PublishesProjectionsWithoutANewFlag()
    {
        var analyze = Assert.Single(CommandFactory.CreateRootCommand().Subcommands);
        var names = analyze.Options
            .Where(static option => option is not HelpOption and not VersionOption)
            .Select(static option => option.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["--output", "--solution"], names);
        Assert.DoesNotContain(names, static name => name.Contains("project", StringComparison.OrdinalIgnoreCase));
    }
}
