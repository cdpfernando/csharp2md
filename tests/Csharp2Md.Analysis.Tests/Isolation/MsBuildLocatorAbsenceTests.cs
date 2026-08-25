namespace Csharp2Md.Analysis.Tests.Isolation;

public sealed class MsBuildLocatorAbsenceTests
{
    [Theory]
    [Trait("Requirement", "ROSE-28")]
    [InlineData("MSBuildLocator")]
    [InlineData("RegisterDefaults")]
    public void ProductionSources_DoNotContainMsBuildLocatorToken(string token)
    {
        var srcRoot = Path.Combine(AnalysisTestPaths.RepoRoot, "src");
        Assert.True(Directory.Exists(srcRoot), $"Production source root was not found at '{srcRoot}'.");

        var offending = Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedOutput(path))
            .FirstOrDefault(path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal));

        Assert.True(
            offending is null,
            $"Production source '{offending}' must not contain '{token}'.");
    }

    private static bool IsGeneratedOutput(string path)
    {
        var segments = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return segments.Contains("obj", StringComparer.OrdinalIgnoreCase)
            || segments.Contains("bin", StringComparer.OrdinalIgnoreCase);
    }
}
