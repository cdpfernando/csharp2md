using System.Diagnostics;

namespace Csharp2Md.Core.Tests.Analysis.Viability;

/// <summary>
/// Executable evidence for process-tree cleanup. The runtime contract is documented at
/// https://learn.microsoft.com/dotnet/api/system.diagnostics.process.kill: passing
/// <c>entireProcessTree: true</c> includes descendants, and parent exit alone does not prove that
/// descendants have exited. The probe therefore verifies every recorded PID before returning.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ProcessTreeProbeTests
{
    [Fact]
    public async Task ServiceTimeout_KillsParentAndDescendantBeforeReturningAndDoesNotCancelNextService()
    {
        using var fixture = ProcessFixture.Create();

        var result = await ProcessTreeProbe.RunAsync(
            fixture,
            TimeSpan.FromSeconds(2),
            CancellationToken.None);

        Assert.True(result.TimedOut);
        Assert.False(IsRunning(result.ParentProcessId));
        Assert.False(IsRunning(result.DescendantProcessId));
        Assert.False(File.Exists(fixture.CompletionMarkerPath));

        var nextServiceExitCode = await ProcessTreeProbe.RunShortServiceAsync(CancellationToken.None);
        Assert.Equal(0, nextServiceExitCode);
    }

    [Fact]
    public async Task CallerCancellation_RemainsCancellationAndKillsParentAndDescendantBeforeReturning()
    {
        using var fixture = ProcessFixture.Create();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ProcessTreeProbe.RunAsync(fixture, TimeSpan.FromMinutes(1), cancellation.Token));

        var processIds = await ProcessTreeProbe.ReadProcessIdsAsync(fixture.ProcessIdsPath, CancellationToken.None);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.False(IsRunning(processIds.ParentProcessId));
        Assert.False(IsRunning(processIds.DescendantProcessId));
        Assert.False(File.Exists(fixture.CompletionMarkerPath));
    }

    private static bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private sealed class ProcessFixture : IDisposable
    {
        private ProcessFixture(string root)
        {
            Root = root;
            ProcessIdsPath = Path.Combine(root, "process-ids.txt");
            CompletionMarkerPath = Path.Combine(root, "completed.marker");
        }

        public string Root { get; }

        public string ProcessIdsPath { get; }

        public string CompletionMarkerPath { get; }

        public static ProcessFixture Create() => new(Directory.CreateTempSubdirectory("c2m-tree-").FullName);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed record ProcessIds(int ParentProcessId, int DescendantProcessId);

    private sealed record TreeProbeResult(bool TimedOut, int ParentProcessId, int DescendantProcessId);

    private static class ProcessTreeProbe
    {
        public static async Task<TreeProbeResult> RunAsync(
            ProcessFixture fixture,
            TimeSpan serviceTimeout,
            CancellationToken cancellationToken)
        {
            var script =
                "$child = Start-Process -FilePath 'powershell.exe' " +
                "-ArgumentList '-NoProfile','-NonInteractive','-Command','Start-Sleep -Seconds 300' " +
                "-PassThru -WindowStyle Hidden; " +
                $"Set-Content -LiteralPath '{Escape(fixture.ProcessIdsPath)}' -Value \"$PID`n$($child.Id)\"; " +
                "Start-Sleep -Seconds 300; " +
                $"Set-Content -LiteralPath '{Escape(fixture.CompletionMarkerPath)}' -Value 'completed'";
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                WorkingDirectory = fixture.Root,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add(script);

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            using var timeout = new CancellationTokenSource(serviceTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);

            try
            {
                var processIds = await ReadProcessIdsAsync(fixture.ProcessIdsPath, linked.Token);
                await process.WaitForExitAsync(linked.Token);
                throw new InvalidOperationException(
                    $"Long-running process exited unexpectedly ({process.ExitCode}): {await stdout}\n{await stderr}");
            }
            catch (OperationCanceledException)
            {
                await TerminateTreeBeforeReturnAsync(process, fixture.ProcessIdsPath);
                await stdout;
                await stderr;

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException(cancellationToken);
                }

                var processIds = await ReadProcessIdsAsync(fixture.ProcessIdsPath, CancellationToken.None);
                return new TreeProbeResult(true, processIds.ParentProcessId, processIds.DescendantProcessId);
            }
        }

        public static async Task<int> RunShortServiceAsync(CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add("exit 0");
            using var process = Process.Start(startInfo)!;
            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode;
        }

        public static async Task<ProcessIds> ReadProcessIdsAsync(string path, CancellationToken cancellationToken)
        {
            while (!File.Exists(path))
            {
                await Task.Delay(25, cancellationToken);
            }

            var values = await File.ReadAllLinesAsync(path, cancellationToken);
            Assert.Equal(2, values.Length);
            return new ProcessIds(int.Parse(values[0]), int.Parse(values[1]));
        }

        private static async Task TerminateTreeBeforeReturnAsync(Process process, string processIdsPath)
        {
            var processIds = await ReadProcessIdsAsync(processIdsPath, CancellationToken.None);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None);
            await WaitUntilExitedAsync(processIds.ParentProcessId);
            await WaitUntilExitedAsync(processIds.DescendantProcessId);
        }

        private static async Task WaitUntilExitedAsync(int processId)
        {
            var deadline = Stopwatch.StartNew();
            while (IsRunning(processId) && deadline.Elapsed < TimeSpan.FromSeconds(10))
            {
                await Task.Delay(25);
            }

            Assert.False(IsRunning(processId));
        }

        private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    }
}
