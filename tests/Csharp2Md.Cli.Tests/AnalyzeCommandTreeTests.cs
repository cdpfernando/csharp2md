using System.CommandLine;
using Csharp2Md.Cli;

namespace Csharp2Md.Cli.Tests;

public sealed class AnalyzeCommandTreeTests
{
    [Fact]
    [Trait("Requirement", "ENG-37")]
    public void RootCommand_ExposesExactlyOneVerbNamedAnalyze()
    {
        var root = CommandFactory.CreateRootCommand();

        var analyze = Assert.Single(root.Subcommands);
        Assert.Equal("analyze", analyze.Name);
        Assert.Null(root.Action);
    }

    [Fact]
    [Trait("Requirement", "ENG-38")]
    public void Analyze_RequiresRepeatableSolutionOptionThatIsNotPositional()
    {
        var analyze = Assert.Single(CommandFactory.CreateRootCommand().Subcommands);
        var solution = Assert.Single(analyze.Options, option => option.Name == "--solution");

        Assert.True(solution.Required);
        Assert.Equal(ArgumentArity.OneOrMore, solution.Arity);
        Assert.Empty(analyze.Arguments);
    }
}
