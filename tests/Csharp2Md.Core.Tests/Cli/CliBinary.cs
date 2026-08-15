namespace Csharp2Md.Core.Tests.Cli;

/// <summary>Locates and builds the CLI so end-to-end tests can invoke the real tool.</summary>
internal static class CliBinary
{
    private static readonly Lazy<Task> Build = new(BuildAsync);

    // Matches whatever configuration this very test assembly was built under (bin/<Config>/net10.0/).
    public static readonly string Configuration =
        Path.GetFileName(Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)))!;

    public static readonly string ProjectPath = Path.Combine(TestPaths.RepoRoot, "src", "Csharp2Md.Cli");

    /// <summary>
    /// The apphost, not the managed dll. The tool must be launchable without <c>dotnet</c> on PATH,
    /// which is exactly the condition one of the end-to-end tests puts it in.
    /// </summary>
    public static readonly string ExecutablePath = Path.Combine(
        ProjectPath,
        "bin",
        Configuration,
        "net10.0",
        OperatingSystem.IsWindows() ? "Csharp2Md.Cli.exe" : "Csharp2Md.Cli");

    /// <summary>
    /// `dotnet test` builds test projects and their references — not sibling non-test projects like
    /// the CLI. Building it explicitly (incremental, a no-op when current) keeps these tests
    /// self-contained under the Full gate.
    /// </summary>
    public static Task EnsureBuiltAsync() => Build.Value;

    private static async Task BuildAsync()
    {
        var build = await ProcessRunner.RunAsync(
            "dotnet", $"build \"{ProjectPath}\" -c {Configuration}", TestPaths.RepoRoot, CancellationToken.None);

        Assert.True(build.ExitCode == 0, $"dotnet build failed:\n{build.StandardOutput}\n{build.StandardError}");
    }
}
