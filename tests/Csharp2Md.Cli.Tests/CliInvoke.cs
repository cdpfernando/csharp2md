using System.CommandLine;
using Csharp2Md.Analysis;
using Csharp2Md.Cli;

namespace Csharp2Md.Cli.Tests;

internal static class CliInvoke
{
    internal static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(
        string[] args,
        IAnalysisEngine? engine = null)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var configuration = new InvocationConfiguration
        {
            Output = stdout,
            Error = stderr,
        };

        var exitCode = await CommandFactory.InvokeAsync(args, engine, configuration);
        return (exitCode, stdout.ToString(), stderr.ToString());
    }
}
