using System.Text;
using System.Text.Json;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Registry;

/// <summary>
/// Projects <see cref="TaxonomyTables"/> to deterministic JSON. Lives in the test project (AD-006):
/// <c>Csharp2Md.Domain</c> must never reference <c>System.Text.Json</c>.
/// </summary>
internal static class TaxonomyRegistryWriter
{
    public static string CommittedPath =>
        Path.Combine(DomainTestPaths.RepoRoot, "contracts", "taxonomy-registry.json");

    public static string Write(TaxonomyTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        // Constructing the registry runs the same duplicate-triple guard the domain enforces at
        // runtime (RelationTripleIndex.Build), so the emitter inherits it instead of re-implementing it.
        _ = new TaxonomyRegistry(tables);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true, IndentSize = 2 }))
        {
            WriteRegistry(writer, tables);
        }

        var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        var json = utf8NoBom.GetString(stream.ToArray());
        return json.Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void WriteRegistry(Utf8JsonWriter writer, TaxonomyTables tables)
    {
        writer.WriteStartObject();

        writer.WriteNumber("schema_version", tables.Versions.SchemaVersion);
        writer.WriteNumber("taxonomy_version", tables.Versions.TaxonomyVersion);
        writer.WriteNumber("observation_schema_version", tables.Versions.ObservationSchemaVersion);
        writer.WriteNumber("extractor_set_version", tables.Versions.ExtractorSetVersion);
        writer.WriteNumber("classifier_set_version", tables.Versions.ClassifierSetVersion);

        WriteFactTypes(writer, tables.FactTypes);
        WriteRelations(writer, tables.Relations);
        WriteStringArray(writer, "mapping_roles", tables.MappingRoles);
        WriteStringArray(writer, "payload_roles", tables.PayloadRoles);
        WriteObservationKinds(writer, tables.ObservationKinds);
        WriteAxes(writer, "facet_axes", tables.FacetAxes);
        WriteAxes(writer, "proof_axes", tables.ProofAxes);

        writer.WriteEndObject();
    }

    private static void WriteFactTypes(Utf8JsonWriter writer, ImmutableArray<FactTypeDescriptor> factTypes)
    {
        writer.WriteStartArray("fact_types");
        foreach (var factType in factTypes)
        {
            writer.WriteStartObject();
            writer.WriteString("family", factType.Family.ToString());
            writer.WriteString("name", factType.Name);
            WriteStringArray(writer, "identity_components", factType.IdentityComponents);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteRelations(Utf8JsonWriter writer, ImmutableArray<RelationDescriptor> relations)
    {
        writer.WriteStartArray("relations");
        foreach (var relation in relations)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", relation.Kind.ToString());
            writer.WriteString("wire_name", relation.WireName);

            writer.WriteStartArray("triples");
            foreach (var triple in relation.Triples)
            {
                writer.WriteStartObject();
                writer.WriteString("source_fact_type", triple.SourceFactType);
                writer.WriteString("target_fact_type", triple.TargetFactType);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteString("minimum_evidence_method", relation.MinimumEvidenceMethod.ToString());
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteObservationKinds(Utf8JsonWriter writer, ImmutableArray<ObservationKindDescriptor> observationKinds)
    {
        writer.WriteStartArray("observation_kinds");
        foreach (var kind in observationKinds)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", kind.Kind.ToString());
            writer.WriteString("wire_name", kind.WireName);
            writer.WriteString("tier", kind.Tier.ToString());
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteAxes(Utf8JsonWriter writer, string propertyName, ImmutableArray<FacetAxisDescriptor> axes)
    {
        writer.WriteStartArray(propertyName);
        foreach (var axis in axes)
        {
            writer.WriteStartObject();
            writer.WriteString("name", axis.Name);
            WriteStringArray(writer, "values", axis.Values);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteStringArray(Utf8JsonWriter writer, string propertyName, ImmutableArray<string> values)
    {
        writer.WriteStartArray(propertyName);
        foreach (var value in values)
        {
            writer.WriteStringValue(value);
        }

        writer.WriteEndArray();
    }
}
