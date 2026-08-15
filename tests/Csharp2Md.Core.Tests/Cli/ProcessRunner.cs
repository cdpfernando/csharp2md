using System.Diagnostics;

namespace Csharp2Md.Core.Tests.Cli;

internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}
