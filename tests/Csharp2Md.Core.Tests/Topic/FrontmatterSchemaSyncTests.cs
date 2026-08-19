using System.Text.Json;
using Csharp2Md.Core.Topic;

namespace Csharp2Md.Core.Tests.Topic;

/// <summary>
/// Keeps <c>schemas/frontmatter.schema.json</c> — the published external contract — in sync with the
/// <see cref="Frontmatter"/> record and <see cref="FileType"/> enum it describes (WIKI-06). The
/// schema is never executed at runtime (design.md's validation-mechanism decision); this test is
/// what stops it from drifting silently.
/// </summary>
public sealed class FrontmatterSchemaSyncTests
{
    private static readonly string SchemaPath =
        Path.Combine(TestPaths.RepoRoot, "schemas", "frontmatter.schema.json");

    [Fact]
    public void RequiredProperties_MatchFrontmatterRecordPropertiesExactly()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SchemaPath));

        var schemaRequired = document.RootElement.GetProperty("required")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var recordFields = typeof(Frontmatter).GetProperties()
            .Select(property => ToSnakeCase(property.Name))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var missingFromSchema = recordFields.Except(schemaRequired, StringComparer.Ordinal).ToArray();
        var extraInSchema = schemaRequired.Except(recordFields, StringComparer.Ordinal).ToArray();

        Assert.True(
            missingFromSchema.Length == 0 && extraInSchema.Length == 0,
            $"frontmatter.schema.json drifted from Frontmatter: missing from schema [{string.Join(", ", missingFromSchema)}], " +
            $"extra in schema [{string.Join(", ", extraInSchema)}]");
    }

    [Fact]
    public void FileTypeEnum_MatchesFileTypeEnumMembersExactly()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SchemaPath));

        var schemaValues = document.RootElement
            .GetProperty("properties").GetProperty("file_type").GetProperty("enum")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var enumValues = Enum.GetNames<FileType>()
            .Select(ToKebabCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var missingFromSchema = enumValues.Except(schemaValues, StringComparer.Ordinal).ToArray();
        var extraInSchema = schemaValues.Except(enumValues, StringComparer.Ordinal).ToArray();

        Assert.True(
            missingFromSchema.Length == 0 && extraInSchema.Length == 0,
            $"frontmatter.schema.json's file_type enum drifted from FileType: missing from schema [{string.Join(", ", missingFromSchema)}], " +
            $"extra in schema [{string.Join(", ", extraInSchema)}]");
    }

    [Fact]
    public void SourceKindEnum_MatchesSourceKindEnumMembersExactly()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(SchemaPath));

        var schemaValues = document.RootElement
            .GetProperty("properties").GetProperty("source_kind").GetProperty("enum")
            .EnumerateArray()
            .Select(element => element.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var enumValues = Enum.GetNames<SourceKind>()
            .Select(ToKebabCase)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var missingFromSchema = enumValues.Except(schemaValues, StringComparer.Ordinal).ToArray();
        var extraInSchema = schemaValues.Except(enumValues, StringComparer.Ordinal).ToArray();

        Assert.True(
            missingFromSchema.Length == 0 && extraInSchema.Length == 0,
            $"frontmatter.schema.json's source_kind enum drifted from SourceKind: missing from schema [{string.Join(", ", missingFromSchema)}], " +
            $"extra in schema [{string.Join(", ", extraInSchema)}]");
    }

    private static string ToSnakeCase(string pascalCase) => ToDelimitedLowerCase(pascalCase, '_');

    private static string ToKebabCase(string pascalCase) => ToDelimitedLowerCase(pascalCase, '-');

    private static string ToDelimitedLowerCase(string pascalCase, char delimiter) =>
        string.Concat(pascalCase.Select((c, i) => i > 0 && char.IsUpper(c) ? $"{delimiter}{c}" : c.ToString()))
            .ToLowerInvariant();
}
