using Csharp2Md.Core.Loading;
using Csharp2Md.Core.Pipeline;

namespace Csharp2Md.Core.Tests.Pipeline;

public sealed class RunReporterTests
{
    [Fact]
    public void Summarize_DegradedProject_ListsItWithARestoreSuggestion()
    {
        var report = new LoadReport([Project("Acme.Broken", ProjectLoadStatus.Degraded)]);

        var summary = RunReporter.Summarize(report);

        Assert.Contains("Acme.Broken (degraded) — suggested fix: dotnet restore", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Summarize_PossibleMissingRestoreProject_ListsItWithARestoreSuggestion()
    {
        var report = new LoadReport([Project("Acme.Payments", ProjectLoadStatus.PossibleMissingRestore)]);

        var summary = RunReporter.Summarize(report);

        Assert.Contains(
            "Acme.Payments (possible missing restore) — suggested fix: dotnet restore",
            summary,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Summarize_DegradedAndMissingRestore_ListsEveryAffectedProjectWithItsOwnSuggestion()
    {
        var report = new LoadReport([
            Project("Acme.Orders", ProjectLoadStatus.Ok),
            Project("Acme.Broken", ProjectLoadStatus.Degraded),
            Project("Acme.Payments", ProjectLoadStatus.PossibleMissingRestore),
        ]);

        var summary = RunReporter.Summarize(report);

        Assert.Equal("Run summary: 2 of 3 project(s) need attention.", Lines(summary)[0]);
        Assert.Equal("  - Acme.Broken (degraded) — suggested fix: dotnet restore", Lines(summary)[1]);
        Assert.Equal(
            "  - Acme.Payments (possible missing restore) — suggested fix: dotnet restore", Lines(summary)[2]);
        Assert.Equal(3, Lines(summary).Count);
    }

    [Fact]
    public void Summarize_HealthyRun_ReportsACleanSummaryWithNoWarnings()
    {
        var report = new LoadReport([
            Project("Acme.Orders", ProjectLoadStatus.Ok),
            Project("Acme.Shared.Contracts", ProjectLoadStatus.Ok),
        ]);

        var summary = RunReporter.Summarize(report);

        Assert.Equal(
            "Run summary: 2 project(s) loaded, none degraded or missing a restore.", Assert.Single(Lines(summary)));
        Assert.DoesNotContain("dotnet restore", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Summarize_UnsupportedForCompilationProject_IsNotReportedAsAFailure()
    {
        // P1-09: not a failure, and no restore can fix it — so it is neither listed nor counted.
        var report = new LoadReport([
            Project("Acme.Orders", ProjectLoadStatus.Ok),
            Project("Acme.Native", ProjectLoadStatus.UnsupportedForCompilation),
        ]);

        var summary = RunReporter.Summarize(report);

        Assert.Equal(
            "Run summary: 2 project(s) loaded, none degraded or missing a restore.", Assert.Single(Lines(summary)));
        Assert.DoesNotContain("Acme.Native", summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Summarize_HealthyProjectsAlongsideAffectedOnes_AreNotListed()
    {
        var report = new LoadReport([
            Project("Acme.Orders", ProjectLoadStatus.Ok),
            Project("Acme.Payments", ProjectLoadStatus.PossibleMissingRestore),
        ]);

        var summary = RunReporter.Summarize(report);

        Assert.DoesNotContain("Acme.Orders", summary, StringComparison.Ordinal);
        Assert.Contains("Acme.Payments", summary, StringComparison.Ordinal);
    }

    private static ProjectLoadResult Project(string name, ProjectLoadStatus status) => new(name, status, []);

    private static IReadOnlyList<string> Lines(string summary) =>
        summary.Split('\n', StringSplitOptions.RemoveEmptyEntries);
}
