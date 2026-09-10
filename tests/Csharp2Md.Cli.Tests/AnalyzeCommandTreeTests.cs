using System.CommandLine;
using Csharp2Md.Cli;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeCommandTreeTests
{
    [Fact]
    [Trait("Requirement", "ENG-37")]
    [Trait("Requirement", "GCPC-063")]
    public void RootCommand_ExposesAnalyzeAndValidate()
    {
        var root = CommandFactory.CreateRootCommand();

        var names = root.Subcommands.Select(static command => command.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
        Assert.Equal(["analyze", "validate"], names);
        Assert.Null(root.Action);
    }

    [Fact]
    [Trait("Requirement", "ENG-38")]
    public void Analyze_RequiresRepeatableSolutionOptionThatIsNotPositional()
    {
        var analyze = Analyze();
        var solution = Assert.Single(analyze.Options, option => option.Name == "--solution");

        Assert.True(solution.Required);
        Assert.Equal(ArgumentArity.OneOrMore, solution.Arity);
        Assert.Empty(analyze.Arguments);
    }

    [Fact]
    [Trait("Requirement", "STOR-47")]
    public void Analyze_RequiresOutputOptionThatIsNotPositional()
    {
        var analyze = Analyze();
        var output = Assert.Single(analyze.Options, option => option.Name == "--output");

        Assert.True(output.Required);
        Assert.Equal(ArgumentArity.ExactlyOne, output.Arity);
        Assert.Empty(analyze.Arguments);
    }

    private static Command Analyze() =>
        Assert.Single(CommandFactory.CreateRootCommand().Subcommands, static command => command.Name == "analyze");
}
