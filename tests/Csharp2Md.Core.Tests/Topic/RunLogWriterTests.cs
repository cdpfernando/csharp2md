using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Topic;

/// <summary>WIKI-18..22: <c>raw/log.md</c>'s required content and its determinism seam.</summary>
public sealed class RunLogWriterTests : IDisposable
{
    private readonly string _outputRoot = Directory.CreateTempSubdirectory("csharp2md-runlog-").FullName;

    public void Dispose() => Directory.Delete(_outputRoot, recursive: true);

    private static readonly DateTimeOffset FixedInstant =
        new(2026, 8, 15, 20, 30, 45, TimeSpan.Zero);

    private static RunLogData Data(IReadOnlyList<FrontmatterFailure>? failures = null) => new(
        Invocation: "csharp2md fixtures/SyntheticSolution --topic acme-shop",
        DocumentCount: 42,
        EdgeCount: 11,
        ServiceCount: 3,
        Failures: failures ?? [],
        OutputTopicPath: "/abs/output/raw");

    private string LogPath => Path.Combine(TopicLayout.RawRoot(_outputRoot), RunLogWriter.FileName);

    // WIKI-18: never DateTimeOffset.UtcNow directly — a fixed FakeClock proves the timestamp comes
    // from the injected TimeProvider, and the exact ISO 8601 formatting is asserted, not just "some
    // timestamp appears".
    [Fact]
    public void Write_TimestampComesFromTheInjectedTimeProvider_AndFormatsAsIso8601Utc()
    {
        RunLogWriter.Write(_outputRoot, Data(), new FakeClock(FixedInstant));

        var content = File.ReadAllText(LogPath);

        Assert.Contains("- Timestamp (UTC): 2026-08-15T20:30:45Z", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_TimestampChangesWithTheProvidedClock_NeverWithWallClockTime()
    {
        var later = FixedInstant.AddDays(1);

        RunLogWriter.Write(_outputRoot, Data(), new FakeClock(later));

        var content = File.ReadAllText(LogPath);

        Assert.Contains("- Timestamp (UTC): 2026-08-16T20:30:45Z", content, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-08-15", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ReportsAllFiveStatistics()
    {
        RunLogWriter.Write(_outputRoot, Data(), new FakeClock(FixedInstant));

        var content = File.ReadAllText(LogPath);

        Assert.Contains("- Invocation: `csharp2md fixtures/SyntheticSolution --topic acme-shop`", content, StringComparison.Ordinal);
        Assert.Contains("- Documents generated: 42", content, StringComparison.Ordinal);
        Assert.Contains("- Dependency edges: 11", content, StringComparison.Ordinal);
        Assert.Contains("- Services analyzed: 3", content, StringComparison.Ordinal);
        Assert.Contains("- Frontmatter validation failures: 0", content, StringComparison.Ordinal);
        Assert.Contains("- Output topic path: /abs/output/raw", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_ContainsEmptyGraphResolutionAndCalibrationNotesSections()
    {
        RunLogWriter.Write(_outputRoot, Data(), new FakeClock(FixedInstant));

        var content = File.ReadAllText(LogPath);

        // "Empty" per WIKI-20: the heading is present with nothing but whitespace before the next
        // heading (or end of file) — not merely that the substring "## Graph Resolution" occurs
        // somewhere.
        Assert.Contains("## Graph Resolution\n\n## Calibration Notes\n", content, StringComparison.Ordinal);
        Assert.EndsWith("## Calibration Notes", content.TrimEnd('\n'), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WithValidationFailures_ListsEachFailingPathAndError()
    {
        IReadOnlyList<FrontmatterFailure> failures =
        [
            new FrontmatterFailure("Acme.Orders/OrderService.cs", "Required field 'domain' is missing or empty."),
            new FrontmatterFailure("Acme.Orders/PaymentsGrpcClient.cs", "Frontmatter does not parse as valid YAML."),
        ];

        RunLogWriter.Write(_outputRoot, Data(failures), new FakeClock(FixedInstant));

        var content = File.ReadAllText(LogPath);

        Assert.Contains("- Frontmatter validation failures: 2", content, StringComparison.Ordinal);
        Assert.Contains(
            "- `Acme.Orders/OrderService.cs`: Required field 'domain' is missing or empty.",
            content, StringComparison.Ordinal);
        Assert.Contains(
            "- `Acme.Orders/PaymentsGrpcClient.cs`: Frontmatter does not parse as valid YAML.",
            content, StringComparison.Ordinal);
    }

    // WIKI-22: writing must never be made contingent on Failures being empty — this is the same call
    // used by every other test here, just with a non-empty failure list, and it must still succeed.
    [Fact]
    public void Write_WithValidationFailures_StillWritesTheFile()
    {
        var path = RunLogWriter.Write(
            _outputRoot,
            Data([new FrontmatterFailure("Acme.Orders/Broken.cs", "bad")]),
            new FakeClock(FixedInstant));

        Assert.True(File.Exists(path));
    }

    private sealed class FakeClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
