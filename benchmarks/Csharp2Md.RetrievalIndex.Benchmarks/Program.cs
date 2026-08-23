using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Csharp2Md.Core.Projection.Aggregates;

namespace Csharp2Md.RetrievalIndex.Benchmarks;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            return args.FirstOrDefault() switch
            {
                "compare" => await CompareAsync(args[1..]).ConfigureAwait(false),
                "sample" => Sample(args[1..]),
                "verify" => Verify(args[1..]),
                _ => Usage(),
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static async Task<int> CompareAsync(string[] args)
    {
        var reportPath = RequiredOption(args, "--report");
        var count = IntOption(args, "--count", 25000);
        var report = await BenchmarkRunner.RunAsync(count, warmups: 1, samples: 3).ConfigureAwait(false);
        var parent = Path.GetDirectoryName(Path.GetFullPath(reportPath));
        if (parent is not null) Directory.CreateDirectory(parent);
        var reportBytes = JsonSerializer.SerializeToUtf8Bytes(report, BenchmarkJsonContext.Default.BenchmarkReport);
        await File.WriteAllBytesAsync(reportPath, [.. reportBytes, (byte)'\n']).ConfigureAwait(false);
        return Report(report.Comparison);
    }

    private static int Sample(string[] args)
    {
        var schema = IntOption(args, "--schema", 0);
        var count = IntOption(args, "--count", 0);
        var output = RequiredOption(args, "--output");
        BenchmarkCorpus.Project(schema, count, output);
        var process = Process.GetCurrentProcess();
        Console.Out.WriteLine($"{Environment.ProcessId}|{process.PeakWorkingSet64}");
        return 0;
    }

    private static int Verify(string[] args)
    {
        var reportPath = RequiredOption(args, "--report");
        var report = JsonSerializer.Deserialize(
            File.ReadAllBytes(reportPath),
            BenchmarkJsonContext.Default.BenchmarkReport)
            ?? throw new JsonException($"Benchmark report '{reportPath}' was null.");
        var schema1 = report.Variants.SingleOrDefault(static variant => variant.IndexSchemaVersion == 1)
            ?? MissingVariant(1);
        var schema2 = report.Variants.SingleOrDefault(static variant => variant.IndexSchemaVersion == 2)
            ?? MissingVariant(2);
        return Report(BenchmarkComparison.Evaluate(schema1, schema2));
    }

    private static BenchmarkVariant MissingVariant(int schema) => new(schema, 0, 0, [], [], 0, 0, []);

    private static int Report(BenchmarkComparison comparison)
    {
        foreach (var failure in comparison.Failures)
        {
            Console.Error.WriteLine($"Schema 2 did not reduce {failure}.");
        }

        return comparison.Failures.IsEmpty ? 0 : 1;
    }

    private static int Usage()
    {
        Console.Error.WriteLine(
            "Usage: compare --report <path> [--count <n>] | sample --schema <1|2> --count <n> --output <path> | verify --report <path>");
        return 1;
    }

    private static int IntOption(string[] args, string name, int fallback)
    {
        var value = Option(args, name);
        if (value is null) return fallback;
        if (!int.TryParse(value, out var result) || result <= 0)
        {
            throw new ArgumentException($"Option '{name}' must be a positive integer.");
        }

        return result;
    }

    private static string RequiredOption(string[] args, string name) =>
        Option(args, name) ?? throw new ArgumentException($"Missing required option '{name}'.");

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}

internal static class BenchmarkRunner
{
    public static async Task<BenchmarkReport> RunAsync(int relationCount, int warmups, int samples)
    {
        if (relationCount <= 0) throw new ArgumentOutOfRangeException(nameof(relationCount));
        if (warmups < 0) throw new ArgumentOutOfRangeException(nameof(warmups));
        if (samples <= 0) throw new ArgumentOutOfRangeException(nameof(samples));

        var variants = ImmutableArray.CreateBuilder<BenchmarkVariant>(2);
        for (var schema = 1; schema <= 2; schema++)
        {
            for (var warmup = 0; warmup < warmups; warmup++)
            {
                _ = await RunSampleAsync(schema, relationCount).ConfigureAwait(false);
            }

            var measured = ImmutableArray.CreateBuilder<BenchmarkSample>(samples);
            for (var sample = 0; sample < samples; sample++)
            {
                measured.Add(await RunSampleAsync(schema, relationCount).ConfigureAwait(false));
            }

            var values = measured.ToImmutable();
            variants.Add(new BenchmarkVariant(
                schema,
                values[0].TotalBytes,
                values[0].FileCount,
                values.Select(static value => value.ElapsedMilliseconds).ToImmutableArray(),
                values.Select(static value => value.PeakWorkingSetBytes).ToImmutableArray(),
                Median(values.Select(static value => value.ElapsedMilliseconds)),
                Median(values.Select(static value => value.PeakWorkingSetBytes)),
                values.Select(static value => value.ProcessId).ToImmutableArray()));
        }

        var completed = variants.ToImmutable();
        return new BenchmarkReport(
            1,
            new BenchmarkCorpusDescription(relationCount, relationCount, relationCount),
            new BenchmarkEnvironment(
                RuntimeInformation.FrameworkDescription,
                RuntimeInformation.OSDescription,
                RuntimeInformation.ProcessArchitecture.ToString()),
            new BenchmarkMethodology("Release", warmups, samples, true),
            completed,
            BenchmarkComparison.Evaluate(completed[0], completed[1]));
    }

    private static async Task<BenchmarkSample> RunSampleAsync(int schema, int relationCount)
    {
        var output = Path.Combine(Path.GetTempPath(), $"csharp2md-index-benchmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(output);
        try
        {
            var assembly = typeof(Program).Assembly.Location;
            var start = new ProcessStartInfo("dotnet",
                $"\"{assembly}\" sample --schema {schema} --count {relationCount} --output \"{output}\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Benchmark child process did not start.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            var stopwatch = Stopwatch.StartNew();
            await process.WaitForExitAsync().ConfigureAwait(false);
            stopwatch.Stop();
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"Schema {schema} benchmark child failed: {await stdout.ConfigureAwait(false)} {await stderr.ConfigureAwait(false)}");
            }

            var childValues = (await stdout.ConfigureAwait(false)).Trim().Split('|');
            var processId = int.Parse(childValues[0], System.Globalization.CultureInfo.InvariantCulture);
            var peakWorkingSet = long.Parse(childValues[1], System.Globalization.CultureInfo.InvariantCulture);
            var index = Path.Combine(output, "raw", "index");
            var files = Directory.EnumerateFiles(index, "*", SearchOption.AllDirectories).ToArray();
            return new BenchmarkSample(
                files.Sum(static path => new FileInfo(path).Length),
                files.Length,
                Math.Max(1, stopwatch.ElapsedMilliseconds),
                peakWorkingSet,
                processId);
        }
        finally
        {
            if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
        }
    }

    private static long Median(IEnumerable<long> values)
    {
        var ordered = values.Order().ToArray();
        if (ordered.Length == 0) throw new InvalidOperationException("A benchmark median requires samples.");
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 1 ? ordered[middle] : checked((ordered[middle - 1] + ordered[middle]) / 2);
    }
}

internal static class BenchmarkCorpus
{
    public static void Project(int schema, int relationCount, string output)
    {
        if (schema is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(schema));
        if (relationCount <= 0) throw new ArgumentOutOfRangeException(nameof(relationCount));
        ArgumentException.ThrowIfNullOrWhiteSpace(output);

        var files = new BenchmarkFiles(output);
        var bytes = CreateRelations(relationCount);
        const string reference = "facts/relation/benchmark.json";
        files.Write($"raw/{reference}", bytes);
        var fragment = new ManifestFragment(
            "relation-benchmark",
            reference,
            Convert.ToHexStringLower(SHA256.HashData(bytes)),
            bytes.Length);
        var manifest = new FactualManifest(
            6,
            "4.0.0",
            new ManifestAnalysis("full", "full"),
            "trusted",
            false,
            "none",
            [],
            new ManifestCoverage(1, 1, 1),
            [fragment],
            [fragment.Sha256]);
        if (schema == 1)
        {
            _ = new Schema1RetrievalIndexProjector().Project(manifest, files);
        }
        else
        {
            _ = new RetrievalIndexProjector().Project(manifest, files);
        }
    }

    private static byte[] CreateRelations(int count)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var json = new Utf8JsonWriter(buffer);
        json.WriteStartObject();
        json.WriteNumber("schema_version", 6);
        json.WriteStartArray("documents");
        json.WriteEndArray();
        json.WriteStartArray("relations");
        for (var index = 0; index < count; index++)
        {
            var ordinal = index.ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
            var relationId = $"relation-{ordinal}";
            json.WriteStartObject();
            json.WriteStartObject("header");
            json.WriteString("id", relationId);
            json.WriteString("resolution", "exact");
            json.WriteStartArray("provenance");
            json.WriteStartObject();
            json.WriteString("engine_version", "4.0.0");
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteStartArray("evidence");
            json.WriteStartObject();
            json.WriteString("document_id", "document-shared");
            json.WriteString("relative_path", "Benchmark.cs");
            json.WriteNumber("start_line", index + 1);
            json.WriteNumber("start_column", 1);
            json.WriteNumber("end_line", index + 1);
            json.WriteNumber("end_column", 2);
            json.WriteBoolean("generated_origin", false);
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteString("relation_id", relationId);
            json.WriteString("source_id", $"source-{ordinal}");
            json.WriteString("target_id", $"target-{ordinal}");
            json.WriteString("partition", "structural");
            json.WriteString("relation_kind", "calls");
            json.WriteString("resolution_method", "exact");
            json.WriteStartArray("details");
            json.WriteStartObject();
            json.WriteString("key", "target_text");
            json.WriteString("value", $"Target.{ordinal}");
            json.WriteEndObject();
            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
        return buffer.WrittenSpan.ToArray();
    }

    private sealed class BenchmarkFiles(string root) : IAggregateFileWriter
    {
        private readonly string _root = Path.GetFullPath(root);
        public void CreateDirectory(string relativePath) => Directory.CreateDirectory(Resolve(relativePath));
        public void Write(string relativePath, byte[] bytes)
        {
            var path = Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }

        public bool Exists(string relativePath) => File.Exists(Resolve(relativePath));
        public Stream OpenRead(string relativePath) => File.OpenRead(Resolve(relativePath));
        private string Resolve(string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Benchmark path escapes output root: {relativePath}");
            }

            return path;
        }
    }
}

internal sealed record BenchmarkSample(
    long TotalBytes,
    int FileCount,
    long ElapsedMilliseconds,
    long PeakWorkingSetBytes,
    int ProcessId);
internal sealed record BenchmarkCorpusDescription(int RelationCount, int UniqueSourceCount, int UniqueTargetCount);
internal sealed record BenchmarkEnvironment(string Runtime, string OperatingSystem, string Architecture);
internal sealed record BenchmarkMethodology(string Configuration, int Warmups, int Samples, bool IsolatedProcesses);
internal sealed record BenchmarkVariant(
    int IndexSchemaVersion,
    long TotalBytes,
    int FileCount,
    ImmutableArray<long> ProjectionMsSamples,
    ImmutableArray<long> PeakWorkingSetBytesSamples,
    long MedianProjectionMs,
    long MedianPeakWorkingSetBytes,
    [property: JsonIgnore] ImmutableArray<int> ProcessIds);
internal sealed record BenchmarkComparison(
    bool BytesReduced,
    bool FilesReduced,
    bool TimeReduced,
    bool MemoryReduced,
    [property: JsonIgnore] ImmutableArray<string> Failures)
{
    public static BenchmarkComparison Evaluate(BenchmarkVariant schema1, BenchmarkVariant schema2)
    {
        var failures = ImmutableArray.CreateBuilder<string>();
        Check(schema1.TotalBytes > 0 && schema2.TotalBytes > 0 && schema2.TotalBytes < schema1.TotalBytes, "total_bytes");
        Check(schema1.FileCount > 0 && schema2.FileCount > 0 && schema2.FileCount < schema1.FileCount, "file_count");
        Check(schema1.MedianProjectionMs > 0 && schema2.MedianProjectionMs > 0 &&
            schema2.MedianProjectionMs < schema1.MedianProjectionMs, "median_projection_ms");
        Check(schema1.MedianPeakWorkingSetBytes > 0 && schema2.MedianPeakWorkingSetBytes > 0 &&
            schema2.MedianPeakWorkingSetBytes < schema1.MedianPeakWorkingSetBytes, "median_peak_working_set_bytes");
        var result = failures.ToImmutable();
        return new BenchmarkComparison(
            !result.Contains("total_bytes"),
            !result.Contains("file_count"),
            !result.Contains("median_projection_ms"),
            !result.Contains("median_peak_working_set_bytes"),
            result);

        void Check(bool condition, string metric)
        {
            if (!condition) failures.Add(metric);
        }
    }
}

internal sealed record BenchmarkReport(
    int SchemaVersion,
    BenchmarkCorpusDescription Corpus,
    BenchmarkEnvironment Environment,
    BenchmarkMethodology Methodology,
    ImmutableArray<BenchmarkVariant> Variants,
    BenchmarkComparison Comparison);

[JsonSerializable(typeof(BenchmarkReport))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower, WriteIndented = true)]
internal partial class BenchmarkJsonContext : JsonSerializerContext;
