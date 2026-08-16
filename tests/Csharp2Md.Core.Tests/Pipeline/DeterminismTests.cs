using Csharp2Md.Core.Pipeline;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Pipeline;

/// <summary>
/// spec.md Success Criteria (determinism): two runs over unchanged input produce byte-identical
/// output beneath <c>raw/</c>, except the timestamp line in <c>raw/log.md</c> — the one intentional
/// source of nondeterminism design.md's Risks table names (<c>RunLogWriter</c>'s injected
/// <see cref="TimeProvider"/>).
/// </summary>
/// <remarks>
/// Invokes <see cref="AnalysisPipeline.RunAsync"/> directly, then the same
/// <see cref="TopicScaffoldWriter"/>/<see cref="RunLogWriter"/> calls <c>Program.cs</c> makes after a
/// successful run (T18's note: those two files are written from the CLI layer, not from
/// <c>AnalysisPipeline</c>). This test does not shell out to the packaged CLI binary: the CLI's own
/// <c>Invocation</c>/<c>OutputTopicPath</c> log lines embed each run's absolute <c>--output</c> path
/// verbatim, which necessarily differs between two physically separate output roots — a real
/// difference, but not one the pipeline's derivation introduces, and one that would land on lines
/// other than the timestamp line no matter how faithfully the CLI is invoked. Constructing a single
/// <see cref="RunLogData"/> and writing it into both output roots (varying only the injected
/// <see cref="TimeProvider"/>) isolates the one difference this test exists to prove is contained.
/// </remarks>
[Trait("Category", "Integration")]
public sealed class DeterminismTests
{
    [Fact]
    public async Task RunAsync_TwoRunsOverUnchangedInput_AreByteIdenticalExceptTheLogTimestampLine()
    {
        var workspace = Directory.CreateTempSubdirectory("csharp2md-determinism-").FullName;
        try
        {
            var manifestPath = FixtureManifest.WriteOverrides(
                workspace, SyntheticFixtureRun.Orders, SyntheticFixtureRun.Payments, SyntheticFixtureRun.SharedContracts);
            var topicOptions = TopicOptions.Create("acme-determinism", "system-design", workspace).Options!;

            var firstOutput = Path.Combine(workspace, "output-1");
            var secondOutput = Path.Combine(workspace, "output-2");

            var firstResult = await RunPipelineAsync(manifestPath, firstOutput, topicOptions);
            var secondResult = await RunPipelineAsync(manifestPath, secondOutput, topicOptions);

            // The pipeline itself (documents, indexes, dependency graph) must already be
            // deterministic over unchanged input; asserted directly rather than assumed, before the
            // file-by-file comparison below relies on it.
            Assert.Equal(firstResult.DocumentCount, secondResult.DocumentCount);
            Assert.Equal(firstResult.Graph.Edges.Count, secondResult.Graph.Edges.Count);
            Assert.Equal(firstResult.ServiceCount, secondResult.ServiceCount);
            Assert.Equal(firstResult.FrontmatterFailures.Count, secondResult.FrontmatterFailures.Count);

            const string toolVersion = "2.0.0-determinism-test";
            TopicScaffoldWriter.Write(firstOutput, topicOptions, toolVersion);
            TopicScaffoldWriter.Write(secondOutput, topicOptions, toolVersion);

            // Same RunLogData instance for both writes: Render() is a pure function of (data,
            // timestamp), so the only line that can possibly differ between the two files below is
            // the one interpolating timestampUtc.
            var logData = new RunLogData(
                "csharp2md --manifest \"manifest.json\" --output \"output\" --topic acme-determinism --domain system-design",
                firstResult.DocumentCount,
                firstResult.Graph.Edges.Count,
                firstResult.ServiceCount,
                firstResult.FrontmatterFailures,
                "/abs/output/raw");

            RunLogWriter.Write(firstOutput, logData, new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
            RunLogWriter.Write(secondOutput, logData, new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 5, TimeSpan.Zero)));

            var firstFiles = RelativeFiles(firstOutput);
            var secondFiles = RelativeFiles(secondOutput);

            // Compares the full file set, not a sample: a new nondeterministic artifact anywhere in
            // the tree (or a missing/extra file between runs) fails right here.
            Assert.Equal(firstFiles, secondFiles);

            var logRelativePath = Path
                .GetRelativePath(firstOutput, Path.Combine(TopicLayout.RawRoot(firstOutput), RunLogWriter.FileName))
                .Replace('\\', '/');
            Assert.Contains(logRelativePath, firstFiles);

            foreach (var relativePath in firstFiles)
            {
                var firstPath = Path.Combine(firstOutput, relativePath);
                var secondPath = Path.Combine(secondOutput, relativePath);

                if (relativePath == logRelativePath)
                {
                    AssertLogIdenticalExceptTimestampLine(firstPath, secondPath);
                }
                else
                {
                    Assert.Equal(File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath));
                }
            }
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    private static Task<PipelineRunResult> RunPipelineAsync(string manifestPath, string outputRoot, TopicOptions options) =>
        new AnalysisPipeline().RunAsync(manifestPath, outputRoot, CancellationToken.None, topicOptions: options);

    private static IReadOnlyList<string> RelativeFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    // WIKI-18/design.md: the only permitted difference is the interpolated timestamp — asserted
    // line-by-line (not just "the files differ somewhere") so a regression elsewhere in the log
    // can't hide behind this one permitted difference.
    private static void AssertLogIdenticalExceptTimestampLine(string firstPath, string secondPath)
    {
        var firstLines = File.ReadAllLines(firstPath);
        var secondLines = File.ReadAllLines(secondPath);

        Assert.Equal(firstLines.Length, secondLines.Length);

        const string timestampPrefix = "- Timestamp (UTC): ";
        for (var i = 0; i < firstLines.Length; i++)
        {
            if (firstLines[i].StartsWith(timestampPrefix, StringComparison.Ordinal)
                && secondLines[i].StartsWith(timestampPrefix, StringComparison.Ordinal))
            {
                Assert.NotEqual(firstLines[i], secondLines[i]); // proves the seam actually varied
                continue;
            }

            Assert.True(
                firstLines[i] == secondLines[i],
                $"raw/log.md line {i} differs outside the timestamp line:\n  first:  {firstLines[i]}\n  second: {secondLines[i]}");
        }
    }

    private sealed class FakeClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }
}
