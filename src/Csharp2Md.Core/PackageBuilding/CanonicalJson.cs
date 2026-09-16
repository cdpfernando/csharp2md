using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Csharp2Md.Core.Analysis;
using Csharp2Md.Core.PackageBuilding;
using Csharp2Md.Core.PackageBuilding.Identity;
using Csharp2Md.Core.PackageBuilding.Rendering;
using Csharp2Md.Core.Publication;

namespace Csharp2Md.Core.PackageBuilding;

internal static class CanonicalJson
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        IndentSize = 2,
        NewLine = "\n",
    };
    private static readonly JsonWriterOptions CompactWriterOptions = new() { Indented = false };

    public static ImmutableArray<byte> Write<T>(T dto) => Write(dto, WriterOptions);

    public static ImmutableArray<byte> WriteCompact<T>(T dto) => Write(dto, CompactWriterOptions);

    private static ImmutableArray<byte> Write<T>(T dto, JsonWriterOptions options)
    {
        var typeInfo = TypeInfo<T>();
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            JsonSerializer.Serialize(writer, dto, typeInfo);
        }

        return Normalize(buffer.WrittenSpan);
    }

    public static T Read<T>(ReadOnlySpan<byte> utf8)
    {
        var value = JsonSerializer.Deserialize(utf8, TypeInfo<T>());
        return value ?? throw new JsonException($"Canonical JSON deserialized to null for '{typeof(T)}'.");
    }

    private static ImmutableArray<byte> Normalize(ReadOnlySpan<byte> utf8)
    {
        var json = Utf8NoBom.GetString(utf8);
        json = json.Replace("\r\n", "\n", StringComparison.Ordinal);
        return Utf8NoBom.GetBytes(json).ToImmutableArray();
    }

    private static JsonTypeInfo<T> TypeInfo<T>()
    {
        var info = CoreJsonContext.Default.GetTypeInfo(typeof(T));
        if (info is not JsonTypeInfo<T> typed)
        {
            throw new NotSupportedException($"'{typeof(T)}' is not registered on {nameof(CoreJsonContext)}.");
        }

        return typed;
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = true,
    IndentSize = 2,
    NewLine = "\n")]
[JsonSerializable(typeof(PackageManifest))]
[JsonSerializable(typeof(SolutionManifestEntry))]
[JsonSerializable(typeof(PackageGenerationPointer))]
[JsonSerializable(typeof(RootsManifestEntry))]
[JsonSerializable(typeof(IndexManifestEntry))]
[JsonSerializable(typeof(JourneyManifestEntry))]
[JsonSerializable(typeof(PublicationMeasurements))]
[JsonSerializable(typeof(FilteredCount))]
[JsonSerializable(typeof(ExtractionMeasurements))]
[JsonSerializable(typeof(PackageCertification))]
[JsonSerializable(typeof(SolutionCertification))]
[JsonSerializable(typeof(JourneyCertification))]
[JsonSerializable(typeof(RetrievalModel))]
[JsonSerializable(typeof(SolutionRetrievalModel))]
[JsonSerializable(typeof(NavigationIndexData))]
[JsonSerializable(typeof(NavigationIndexEntry))]
[JsonSerializable(typeof(EvidenceIndexData))]
[JsonSerializable(typeof(EvidenceShardEntry))]
[JsonSerializable(typeof(DependencyPayload))]
[JsonSerializable(typeof(StoredRelation))]
[JsonSerializable(typeof(AggregatedDependency))]
[JsonSerializable(typeof(ScopeMeasures))]
[JsonSerializable(typeof(GapCounts))]
[JsonSerializable(typeof(ImpactTarget))]
[JsonSerializable(typeof(EntityHandle))]
[JsonSerializable(typeof(VariantHandle))]
[JsonSerializable(typeof(RelationHandle))]
[JsonSerializable(typeof(EvidenceHandle))]
[JsonSerializable(typeof(CycleHandle))]
[JsonSerializable(typeof(SolutionIdentity))]
[JsonSerializable(typeof(SolutionId))]
[JsonSerializable(typeof(EvidenceRecord))]
[JsonSerializable(typeof(EngineDiagnostic))]
[JsonSerializable(typeof(ImmutableArray<SolutionManifestEntry>))]
[JsonSerializable(typeof(RootsIndexData))]
[JsonSerializable(typeof(RootIndexEntry))]
[JsonSerializable(typeof(ImmutableArray<RootIndexEntry>))]
[JsonSerializable(typeof(ImmutableArray<IndexManifestEntry>))]
[JsonSerializable(typeof(ImmutableArray<JourneyManifestEntry>))]
[JsonSerializable(typeof(ImmutableArray<FilteredCount>))]
[JsonSerializable(typeof(ImmutableArray<JourneyCertification>))]
[JsonSerializable(typeof(ImmutableArray<SolutionCertification>))]
[JsonSerializable(typeof(ImmutableArray<SolutionRetrievalModel>))]
[JsonSerializable(typeof(ImmutableArray<AggregatedDependency>))]
[JsonSerializable(typeof(ImmutableArray<ScopeMeasures>))]
[JsonSerializable(typeof(ImmutableArray<ImpactTarget>))]
[JsonSerializable(typeof(ImmutableArray<EntityHandle>))]
[JsonSerializable(typeof(ImmutableArray<VariantHandle>))]
[JsonSerializable(typeof(ImmutableArray<RelationHandle>))]
[JsonSerializable(typeof(ImmutableArray<EvidenceHandle>))]
[JsonSerializable(typeof(ImmutableArray<CycleHandle>))]
[JsonSerializable(typeof(ImmutableArray<SolutionIdentity>))]
[JsonSerializable(typeof(ImmutableArray<EvidenceShardEntry>))]
[JsonSerializable(typeof(ImmutableArray<EvidenceRecord>))]
internal partial class CoreJsonContext : JsonSerializerContext;
