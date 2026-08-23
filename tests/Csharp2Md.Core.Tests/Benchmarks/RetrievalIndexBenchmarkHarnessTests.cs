using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    [Fact]
    public void SchemaTwo_TenAndTenThousandUniqueKeysCreateTheSameDirectorySet()
    {
        var tenRoot = Directory.CreateTempSubdirectory("csharp2md-index-directories-10-").FullName;
        var tenThousandRoot = Directory.CreateTempSubdirectory("csharp2md-index-directories-10000-").FullName;
        try
        {
            BenchmarkCorpus.Project(2, 10, tenRoot);
            BenchmarkCorpus.Project(2, 10000, tenThousandRoot);

            var ten = IndexDirectories(tenRoot);
            var tenThousand = IndexDirectories(tenThousandRoot);

            Assert.Equal(ten, tenThousand);
            Assert.Equal(
                ["catalogues", "metadata", "postings", "postings/kind", "postings/resolution", "postings/source", "postings/target", "relations"],
                ten);
        }
        finally
        {
            Directory.Delete(tenRoot, recursive: true);
            Directory.Delete(tenThousandRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("equal")]
    [InlineData("regressed")]
    public async Task VerifyCommand_MissingEqualOrRegressedMetricsExitNonZeroAndNameEveryMetric(string scenario)
    {
        var root = Directory.CreateTempSubdirectory("csharp2md-index-benchmark-failure-").FullName;
        try
        {
            var reportPath = Path.Combine(root, "report.json");
            var schema1 = Variant(1, bytes: 100, files: 10, time: 20, memory: 30);
            var schema2 = scenario == "regressed"
                ? Variant(2, bytes: 101, files: 11, time: 21, memory: 31)
                : Variant(2, bytes: 100, files: 10, time: 20, memory: 30);
            var report = new BenchmarkReport(
                1,
                new BenchmarkCorpusDescription(1, 1, 1),
                new BenchmarkEnvironment("runtime", "os", "architecture"),
                new BenchmarkMethodology("Release", 0, 3, true),
                [schema1, schema2],
                BenchmarkComparison.Evaluate(schema1, schema2));
            var json = JsonNode.Parse(JsonSerializer.SerializeToUtf8Bytes(
                report, BenchmarkJsonContext.Default.BenchmarkReport))!.AsObject();
            if (scenario == "missing")
            {
                var compact = json["variants"]![1]!.AsObject();
                compact.Remove("total_bytes");
                compact.Remove("file_count");
                compact.Remove("median_projection_ms");
                compact.Remove("median_peak_working_set_bytes");
            }

            File.WriteAllText(reportPath, json.ToJsonString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var start = new ProcessStartInfo(
                "dotnet",
                $"\"{typeof(BenchmarkRunner).Assembly.Location}\" verify --report \"{reportPath}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Benchmark verifier did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            Assert.NotEqual(0, process.ExitCode);
            Assert.Empty(await stdout);
            var error = await stderr;
            Assert.Contains("total_bytes", error, StringComparison.Ordinal);
            Assert.Contains("file_count", error, StringComparison.Ordinal);
            Assert.Contains("median_projection_ms", error, StringComparison.Ordinal);
            Assert.Contains("median_peak_working_set_bytes", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] IndexDirectories(string root)
    {
        var indexRoot = Path.Combine(root, "raw", "index");
        return Directory.EnumerateDirectories(indexRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(indexRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
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
