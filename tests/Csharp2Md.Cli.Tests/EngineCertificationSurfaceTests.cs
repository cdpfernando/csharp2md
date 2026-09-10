using System.CommandLine;
using Csharp2Md.Cli;

namespace Csharp2Md.Cli.Tests;

/// <summary>
/// GCPC-081: engine certification is a repository test suite measured against labeled corpora
/// (D-03) -- it must never be exposed as a CLI subcommand or as a flag on an existing one.
/// </summary>
public sealed class EngineCertificationSurfaceTests
{
    [Fact]
    [Trait("Requirement", "GCPC-081")]
    public void CreateRootCommand_ExposesNoCertifySubcommandAndNoEngineCertificationFlag()
    {
        var root = CommandFactory.CreateRootCommand();

        Assert.DoesNotContain(
            root.Subcommands,
            command => command.Name.Contains("certify", StringComparison.OrdinalIgnoreCase)
                || command.Name.Contains("certification", StringComparison.OrdinalIgnoreCase));

        foreach (var subcommand in root.Subcommands)
        {
            Assert.DoesNotContain(
                subcommand.Options,
                option => option.Name.Contains("certify", StringComparison.OrdinalIgnoreCase)
                    || option.Name.Contains("certification", StringComparison.OrdinalIgnoreCase));
        }
    }
}
