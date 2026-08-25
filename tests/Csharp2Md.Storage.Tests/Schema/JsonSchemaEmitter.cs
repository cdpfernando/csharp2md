using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Schema;

internal static class JsonSchemaEmitter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
    };

    public static readonly string[] RelationRecordNames =
    [
        "ConfirmedRelation",
        "CandidateLink",
        "UnresolvedRecord",
        "OpenFrontier",
    ];

    public static readonly string[] EnvelopeNames =
    [
        "manifest",
        "coverage",
        "run_certification",
        "diagnostics",
        "quarantine",
        "measurements",
    ];

    public static string FactSchemaPath(string factTypeName) =>
        Path.Combine("contracts", "json-schema", "facts", factTypeName + ".json");

    public static string ObservationSchemaPath(string wireName) =>
        Path.Combine("contracts", "json-schema", "observations", wireName + ".json");

    public static string RelationSchemaPath(string recordName) =>
        Path.Combine("contracts", "json-schema", "relations", recordName + ".json");

    public static string EnvelopeSchemaPath(string envelopeName) =>
        Path.Combine("contracts", "json-schema", "envelopes", envelopeName + ".json");

    public static IReadOnlyList<(string RelativePath, byte[] Bytes)> EmitAll()
    {
        var emitted = new List<(string RelativePath, byte[] Bytes)>();

        foreach (var factType in TaxonomyTables.Default.FactTypes)
        {
            var dtoType = ResolveWireType(factType.Name + "Dto");
            emitted.Add((FactSchemaPath(factType.Name), EmitSchema(dtoType)));
        }

        var observationBytes = EmitSchema(typeof(ObservationDto));
        foreach (var kind in TaxonomyTables.Default.ObservationKinds)
        {
            emitted.Add((ObservationSchemaPath(kind.WireName), observationBytes));
        }

        emitted.Add((RelationSchemaPath("ConfirmedRelation"), EmitSchema(typeof(ConfirmedRelationDto))));
        emitted.Add((RelationSchemaPath("CandidateLink"), EmitSchema(typeof(CandidateLinkDto))));
        emitted.Add((RelationSchemaPath("UnresolvedRecord"), EmitSchema(typeof(UnresolvedRecordDto))));
        emitted.Add((RelationSchemaPath("OpenFrontier"), EmitSchema(typeof(OpenFrontierDto))));

        emitted.Add((EnvelopeSchemaPath("manifest"), EmitSchema(typeof(ManifestEnvelope))));
        emitted.Add((EnvelopeSchemaPath("coverage"), EmitSchema(typeof(CoverageEnvelope))));
        emitted.Add((EnvelopeSchemaPath("run_certification"), EmitSchema(typeof(RunCertificationEnvelope))));
        emitted.Add((EnvelopeSchemaPath("diagnostics"), EmitSchema(typeof(DiagnosticsEnvelope))));
        emitted.Add((EnvelopeSchemaPath("quarantine"), EmitSchema(typeof(QuarantineEnvelope))));
        emitted.Add((EnvelopeSchemaPath("measurements"), EmitSchema(typeof(MeasurementsEnvelope))));

        return emitted;
    }

    public static void WriteCommitted(string repoRoot)
    {
        foreach (var (relativePath, bytes) in EmitAll())
        {
            var path = Path.Combine(repoRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, bytes);
        }
    }

    public static string DescribeMismatch(string relativePath, byte[] expected, byte[] actual)
    {
        var normalized = relativePath.Replace('\\', '/');
        var expectedLines = Encoding.UTF8.GetString(expected).Split('\n');
        var actualLines = Encoding.UTF8.GetString(actual).Split('\n');

        var differences = new List<string>();
        var lineCount = Math.Max(expectedLines.Length, actualLines.Length);
        for (var i = 0; i < lineCount; i++)
        {
            var expectedLine = i < expectedLines.Length ? expectedLines[i] : "<missing>";
            var actualLine = i < actualLines.Length ? actualLines[i] : "<missing>";
            if (!string.Equals(expectedLine, actualLine, StringComparison.Ordinal))
            {
                differences.Add($"line {i + 1}: expected '{expectedLine}' but found '{actualLine}'");
            }
        }

        var detail = differences.Count == 0 ? "(no line-level differences found)" : string.Join("; ", differences);
        return $"schema drifted: {normalized}: {detail}";
    }

    private static byte[] EmitSchema(Type dtoType)
    {
        var typeInfo = StorageJsonContext.Default.GetTypeInfo(dtoType)
            ?? throw new InvalidOperationException($"'{dtoType}' is not registered on {nameof(StorageJsonContext)}.");

        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(typeInfo);
        var root = schema as JsonObject ?? new JsonObject { ["schema"] = schema.DeepClone() };
        if (root.ContainsKey("schema_version"))
        {
            root.Remove("schema_version");
        }

        root.Insert(0, "schema_version", JsonValue.Create(1));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            root.WriteTo(writer);
        }

        var json = Utf8NoBom.GetString(stream.ToArray());
        json = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Utf8NoBom.GetBytes(json);
    }

    private static Type ResolveWireType(string typeName)
    {
        var type = typeof(StorageJsonContext).Assembly.GetType("Csharp2Md.Storage.Wire." + typeName);
        return type ?? throw new InvalidOperationException($"Wire DTO '{typeName}' was not found.");
    }
}
