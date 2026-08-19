using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Output;

namespace Csharp2Md.Core.Projection.Aggregates;

internal interface IAggregateFileWriter
{
    void CreateDirectory(string relativePath);
    void Write(string relativePath, byte[] bytes);
    bool Exists(string relativePath);
    byte[] Read(string relativePath);
}

internal sealed class CanonicalAggregateWriter(IAggregateFileWriter? files = null)
{
    private static readonly string[] RelationPartitions =
        ["compile-time", "inheritance", "dependency-injection", "http", "grpc", "events"];

    private IAggregateFileWriter? _files = files;

    public AggregateWriteResult Write(
        string outputRoot,
        string inputRoot,
        bool force,
        AggregateOutputSnapshot snapshot,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(inputRoot);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(timeProvider);

        new OutputWriter(outputRoot).PrepareRun(inputRoot, force);
        return WritePrepared(outputRoot, snapshot, timeProvider);
    }

    internal AggregateWriteResult WritePrepared(
        string outputRoot,
        AggregateOutputSnapshot snapshot,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputRoot);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _files ??= new LocalAggregateFileWriter(outputRoot);
        var files = _files;
        files.CreateDirectory("raw/facts/projects");
        files.CreateDirectory("raw/facts/documents");
        files.CreateDirectory("raw/facts/symbols");
        files.CreateDirectory("raw/facts/relations");
        files.CreateDirectory("raw/codebase");

        files.Write("raw/facts/solutions.json", Json(new AggregateEnvelope(2, "solutions", [])));
        var relationProjection = snapshot.Relations;
        foreach (var partition in RelationPartitions)
        {
            var projected = relationProjection?.Partition(ParsePartition(partition));
            files.Write($"raw/facts/relations/{partition}.json", Json(new RelationAggregate(2, partition, projected?.Relations ?? [])));
        }

        var honestCoverage = snapshot.HonestCoverage ?? CoverageProjectionResult.Empty;
        files.Write("raw/facts/diagnostics.json", Json(new DiagnosticAggregate(2, "diagnostics", honestCoverage.Diagnostics)));
        files.Write("raw/facts/coverage.json", Json(new CoverageAggregate(
            2,
            "coverage",
            honestCoverage.Coverage.Select(Map).ToImmutableArray())));
        files.Write("raw/dependencies.mmd", Utf8(relationProjection?.Mermaid ?? "flowchart LR\n"));
        files.Write("raw/codebase/components.md", Utf8(relationProjection?.ComponentIndex ?? "# Components\n"));
        files.Write("raw/topic.yaml", Utf8(TopicYaml(snapshot)));
        files.Write("raw/CLAUDE.md", Utf8("# Generated codebase topic\n\nStart with `facts/manifest.json`.\n"));
        files.Write("raw/log.md", Utf8(Log(snapshot, timeProvider.GetUtcNow())));

        var fragments = ValidateFragments(files, snapshot.Fragments);
        var manifest = new FactualManifest(
            2,
            snapshot.ToolVersion,
            new ManifestAnalysis(Wire(snapshot.RequestedAnalysis), Wire(snapshot.EffectiveAnalysis)),
            Wire(snapshot.Trust),
            RestorePerformed: false,
            Isolation: "none",
            snapshot.Extensions.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray(),
            snapshot.Coverage,
            fragments,
            fragments.Select(static fragment => fragment.Sha256).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray());

        // The manifest is the commit marker for the factual tree and is deliberately last.
        files.Write("raw/facts/manifest.json", Json(manifest));
        return new AggregateWriteResult(Path.Combine(outputRoot, "raw"), Path.Combine(outputRoot, "raw", "facts", "manifest.json"));
    }

    private static ImmutableArray<ManifestFragment> ValidateFragments(
        IAggregateFileWriter files,
        ImmutableArray<StoredFactFragment> fragments)
    {
        var result = ImmutableArray.CreateBuilder<ManifestFragment>(fragments.Length);
        foreach (var fragment in fragments.OrderBy(static fragment => fragment.RootFactId.Value, StringComparer.Ordinal))
        {
            var relativePath = $"raw/{fragment.Reference.Value}";
            if (!files.Exists(relativePath))
            {
                throw new InvalidOperationException($"Persisted fragment is missing: {fragment.Reference.Value}");
            }

            var bytes = files.Read(relativePath);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            if (!string.Equals(hash, fragment.Sha256, StringComparison.Ordinal) || bytes.Length != fragment.ByteLength)
            {
                throw new InvalidOperationException($"Persisted fragment hash or length is invalid: {fragment.Reference.Value}");
            }

            result.Add(new ManifestFragment(
                fragment.RootFactId.Value,
                fragment.Reference.Value,
                fragment.Sha256,
                fragment.ByteLength));
        }

        return result.ToImmutable();
    }

    private static byte[] Json(AggregateEnvelope value) => Serialize(value, AggregateJsonContext.Default.AggregateEnvelope);
    private static byte[] Json(DiagnosticAggregate value) => Serialize(value, AggregateJsonContext.Default.DiagnosticAggregate);
    private static byte[] Json(CoverageAggregate value) => Serialize(value, AggregateJsonContext.Default.CoverageAggregate);
    private static byte[] Json(RelationAggregate value) => Serialize(value, AggregateJsonContext.Default.RelationAggregate);
    private static byte[] Json(FactualManifest value) => Serialize(value, AggregateJsonContext.Default.FactualManifest);

    private static byte[] Serialize<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        var json = JsonSerializer.Serialize(value, typeInfo).Replace("\r\n", "\n", StringComparison.Ordinal);
        return Utf8(json.EndsWith('\n') ? json : json + '\n');
    }

    private static string TopicYaml(AggregateOutputSnapshot snapshot) =>
        $"schema_version: 2\ntopic: {snapshot.Topic}\ndomain: {snapshot.Domain}\ntool_version: {snapshot.ToolVersion}\n";

    private static string Log(AggregateOutputSnapshot snapshot, DateTimeOffset now) =>
        $"# csharp2md audit log\n\n- generated_at: {now:O}\n- requested_analysis: {Wire(snapshot.RequestedAnalysis)}\n"
        + $"- effective_analysis: {Wire(snapshot.EffectiveAnalysis)}\n- trust: {Wire(snapshot.Trust)}\n"
        + "- restore_performed: false\n- isolation: none\n"
        + $"- diagnostics: {snapshot.HonestCoverage?.Summary.DiagnosticCount ?? 0}\n"
        + $"- coverage_scopes: {snapshot.HonestCoverage?.Summary.TotalScopes ?? 0}\n"
        + $"- degraded_scopes: {snapshot.HonestCoverage?.Summary.DegradedScopes ?? 0}\n";

    private static CoverageFactJson Map(CoverageFact coverage) => new(
        coverage.ScopeId.Value,
        Wire(coverage.FactLevel),
        coverage.DetectorId?.Value,
        Wire(coverage.Applicability),
        Wire(coverage.Attempt),
        Wire(coverage.Resolution),
        coverage.DiagnosticIds.Select(static id => id.Value).ToImmutableArray());

    private static byte[] Utf8(string value) => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(value.Replace("\r\n", "\n", StringComparison.Ordinal));
    private static string Wire<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant()
            .Replace("syntaxonly", "syntax-only", StringComparison.Ordinal)
            .Replace("notapplicable", "not-applicable", StringComparison.Ordinal)
            .Replace("notattempted", "not-attempted", StringComparison.Ordinal);

    private static RelationPartition ParsePartition(string partition) => partition switch
    {
        "compile-time" => RelationPartition.CompileTime,
        "inheritance" => RelationPartition.Inheritance,
        "dependency-injection" => RelationPartition.DependencyInjection,
        "http" => RelationPartition.Http,
        "grpc" => RelationPartition.Grpc,
        "events" => RelationPartition.Events,
        _ => throw new ArgumentOutOfRangeException(nameof(partition), partition, "Unsupported relation partition."),
    };

    private sealed class LocalAggregateFileWriter(string root) : IAggregateFileWriter
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
        public byte[] Read(string relativePath) => File.ReadAllBytes(Resolve(relativePath));

        private string Resolve(string relativePath)
        {
            var path = Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = _root + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Aggregate path escapes output root: {relativePath}");
            }

            return path;
        }
    }
}
