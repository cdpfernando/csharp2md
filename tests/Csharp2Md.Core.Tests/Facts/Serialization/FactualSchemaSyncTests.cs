using System.Text.Json;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Serialization;

namespace Csharp2Md.Core.Tests.Facts.Serialization;

public sealed class FactualSchemaSyncTests
{
    private static readonly string SchemaPath = Path.Combine(TestPaths.RepoRoot, "schemas", "facts.schema.json");

    public static TheoryData<string, Type> RecordDefinitions => new()
    {
        { "solution_fact", typeof(SolutionFactJson) },
        { "project_fact", typeof(ProjectFactJson) },
        { "target_fact", typeof(TargetFactJson) },
        { "document_fact", typeof(DocumentFactJson) },
        { "source_section_fact", typeof(SourceSectionFactJson) },
        { "symbol_fact", typeof(SymbolFactJson) },
        { "component_fact", typeof(ComponentFactJson) },
        { "relation_fact", typeof(RelationFactJson) },
        { "database_object_fact", typeof(DatabaseObjectFactJson) },
        { "database_column_fact", typeof(DatabaseColumnFactJson) },
        { "analysis_diagnostic", typeof(AnalysisDiagnosticJson) },
        { "coverage_fact", typeof(CoverageFactJson) },
    };

    public static TheoryData<string, Type> EnumDefinitions => new()
    {
        { "fact_resolution", typeof(FactResolution) },
        { "fact_kind", typeof(FactKind) },
        { "relation_partition", typeof(RelationPartition) },
        { "resolution_method", typeof(ResolutionMethod) },
        { "database_object_kind", typeof(DatabaseObjectKind) },
        { "fact_level", typeof(FactLevel) },
        { "coverage_applicability", typeof(CoverageApplicability) },
        { "coverage_attempt", typeof(CoverageAttempt) },
        { "diagnostic_stage", typeof(DiagnosticStage) },
        { "diagnostic_severity", typeof(DiagnosticSeverity) },
    };

    [Fact]
    public void RootRequiredProperties_MatchFactualDocumentExactlyAndSchemaVersionIsSix()
    {
        using var schema = OpenSchema();
        var required = Required(schema.RootElement);
        var contract = typeof(FactualJsonDocument).GetProperties().Select(static property => Snake(property.Name)).Order().ToArray();

        Assert.Equal(contract, required);
        Assert.Equal(6, schema.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetInt32());
        Assert.Equal(FactualJsonSerializer.SchemaVersion, schema.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetInt32());
    }

    [Fact]
    public void HeaderAndRelationResolutionSchema_RequireGeneratedOriginAndDocumentDistinctMeanings()
    {
        using var schema = OpenSchema();
        var definitions = schema.RootElement.GetProperty("$defs");
        var header = definitions.GetProperty("header");
        var evidence = definitions.GetProperty("evidence");
        var relation = definitions.GetProperty("relation_fact");

        Assert.Equal(
            ["diagnostic_ids", "evidence", "generated_origin", "id", "kind", "provenance", "resolution"],
            Required(header));
        Assert.Equal(
            ["document_id", "end_column", "end_line", "generated_origin", "relative_path", "start_column", "start_line"],
            Required(evidence));
        Assert.Equal("Proof quality of this fact.", header.GetProperty("properties").GetProperty("resolution").GetProperty("description").GetString());
        Assert.Equal("Method used to resolve this relation target.", relation.GetProperty("properties").GetProperty("resolution_method").GetProperty("description").GetString());
    }

    [Theory]
    [MemberData(nameof(RecordDefinitions))]
    public void FactDefinitionRequiredProperties_MatchContractRecordExactly(string definition, Type contractType)
    {
        using var schema = OpenSchema();
        var required = schema.RootElement.GetProperty("$defs").GetProperty(definition).GetProperty("required")
            .EnumerateArray().Select(static item => item.GetString()!).Order().ToArray();
        var contract = contractType.GetProperties()
            .Where(static property => Nullable.GetUnderlyingType(property.PropertyType) is null &&
                !(property.PropertyType.IsClass && property.CustomAttributes.Any(attribute => attribute.AttributeType.Name == "NullableAttribute")))
            .Select(static property => Snake(property.Name)).Order().ToArray();

        Assert.Equal(contract, required);
    }

    [Theory]
    [MemberData(nameof(EnumDefinitions))]
    public void SchemaEnumValues_MatchDomainEnumMembersExactly(string definition, Type enumType)
    {
        using var schema = OpenSchema();
        var schemaValues = schema.RootElement.GetProperty("$defs").GetProperty(definition).GetProperty("enum")
            .EnumerateArray().Select(static item => item.GetString()!).Order().ToArray();
        var domainValues = Enum.GetNames(enumType).Select(Kebab).Order().ToArray();

        Assert.Equal(domainValues, schemaValues);
    }

    private static JsonDocument OpenSchema() => JsonDocument.Parse(File.ReadAllText(SchemaPath));

    private static string[] Required(JsonElement root) => root.GetProperty("required")
        .EnumerateArray().Select(static item => item.GetString()!).Order().ToArray();

    private static string Snake(string value) => Delimit(value, '_');

    private static string Kebab(string value) => Delimit(value, '-');

    private static string Delimit(string value, char delimiter) =>
        string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? $"{delimiter}{character}" : character.ToString()))
            .ToLowerInvariant();
}
