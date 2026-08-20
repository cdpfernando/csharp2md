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
        { "analysis_diagnostic", typeof(AnalysisDiagnosticJson) },
        { "coverage_fact", typeof(CoverageFactJson) },
    };

    public static TheoryData<string, Type> EnumDefinitions => new()
    {
        { "fact_resolution", typeof(FactResolution) },
        { "fact_kind", typeof(FactKind) },
        { "relation_partition", typeof(RelationPartition) },
        { "fact_level", typeof(FactLevel) },
        { "coverage_applicability", typeof(CoverageApplicability) },
        { "coverage_attempt", typeof(CoverageAttempt) },
        { "diagnostic_stage", typeof(DiagnosticStage) },
        { "diagnostic_severity", typeof(DiagnosticSeverity) },
    };

    [Fact]
    public void RootRequiredProperties_MatchFactualDocumentExactlyAndSchemaVersionIsThree()
    {
        using var schema = OpenSchema();
        var required = Required(schema.RootElement);
        var contract = typeof(FactualJsonDocument).GetProperties().Select(static property => Snake(property.Name)).Order().ToArray();

        Assert.Equal(contract, required);
        Assert.Equal(3, schema.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetInt32());
        Assert.Equal(FactualJsonSerializer.SchemaVersion, schema.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetInt32());
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
