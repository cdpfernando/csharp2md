using System.Text.Json;
using Csharp2Md.RetrievalIndex.Benchmarks;

namespace Csharp2Md.Core.Tests.Benchmarks;

public sealed class RetrievalIndexBenchmarkHarnessTests
{
    [Fact]
    public async Task SmallCorpus_UsesFreshProcessesAndRetainsEveryRawSample()
    {
        var report = await BenchmarkRunner.RunAsync(relationCount: 24, warmups: 1, samples: 3);

        Assert.Equal(1, report.SchemaVersion);
        Assert.Equal(24, report.Corpus.RelationCount);
        Assert.Equal(24, report.Corpus.UniqueSourceCount);
        Assert.Equal(24, report.Corpus.UniqueTargetCount);
        Assert.Equal("Release", report.Methodology.Configuration);
        Assert.Equal(1, report.Methodology.Warmups);
        Assert.Equal(3, report.Methodology.Samples);
        Assert.True(report.Methodology.IsolatedProcesses);
        Assert.Equal([1, 2], report.Variants.Select(static variant => variant.IndexSchemaVersion));
        Assert.All(report.Variants, static variant =>
        {
            Assert.Equal(3, variant.ProjectionMsSamples.Length);
            Assert.Equal(3, variant.PeakWorkingSetBytesSamples.Length);
            Assert.Equal(3, variant.ProcessIds.Distinct().Count());
            Assert.All(variant.ProjectionMsSamples, static value => Assert.True(value > 0));
            Assert.All(variant.PeakWorkingSetBytesSamples, static value => Assert.True(value > 0));
            Assert.True(variant.TotalBytes > 0);
            Assert.True(variant.FileCount > 0);
        });
        var processIds = report.Variants.SelectMany(static variant => variant.ProcessIds).ToArray();
        Assert.Equal(processIds.Length, processIds.Distinct().Count());

        using var json = JsonDocument.Parse(
            JsonSerializer.SerializeToUtf8Bytes(report, BenchmarkJsonContext.Default.BenchmarkReport));
        Assert.False(json.RootElement.GetProperty("comparison").TryGetProperty("failures", out _));
        Assert.Equal(3, json.RootElement.GetProperty("variants")[0].GetProperty("projection_ms_samples").GetArrayLength());
    }

    [Fact]
    public void Comparison_RequiresEveryMetricToBePresentAndStrictlyLower()
    {
        var schema1 = Variant(1, bytes: 100, files: 10, time: 20, memory: 30);
        var equalOrWorse = Variant(2, bytes: 100, files: 11, time: 20, memory: 31);

        var failed = BenchmarkComparison.Evaluate(schema1, equalOrWorse);
        var missing = BenchmarkComparison.Evaluate(schema1, Variant(2, bytes: 0, files: 0, time: 0, memory: 0));
        var passed = BenchmarkComparison.Evaluate(schema1, Variant(2, bytes: 99, files: 9, time: 19, memory: 29));

        string[] expectedFailures =
            ["total_bytes", "file_count", "median_projection_ms", "median_peak_working_set_bytes"];
        Assert.Equal(expectedFailures, failed.Failures.ToArray());
        Assert.Equal(expectedFailures, missing.Failures.ToArray());
        Assert.False(failed.BytesReduced);
        Assert.False(failed.FilesReduced);
        Assert.False(failed.TimeReduced);
        Assert.False(failed.MemoryReduced);
        Assert.Empty(passed.Failures);
        Assert.True(passed.BytesReduced);
        Assert.True(passed.FilesReduced);
        Assert.True(passed.TimeReduced);
        Assert.True(passed.MemoryReduced);
    }

    private static BenchmarkVariant Variant(
        int schema,
        long bytes,
        int files,
        long time,
        long memory) => new(
        schema,
        bytes,
        files,
        [time - 1, time, time + 1],
        [memory - 1, memory, memory + 1],
        time,
        memory,
        [1, 2, 3]);
}
