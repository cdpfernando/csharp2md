using System.Text.Json;
using Csharp2Md.Core.Analysis.Syntax;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;
using Csharp2Md.Core.Projection.Markdown;
using VerifyXunit;

namespace Csharp2Md.Core.Tests.Projection.Markdown;

public sealed class FrontmatterV2Tests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("csharp2md-frontmatter-v2-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Create_EmitsOnlySchemaVersionTwoTopLevelFields()
    {
        var frontmatter = Create();

        var keys = TopLevelKeys(frontmatter.ToYaml());

        Assert.Equal(
            ["schema_version", "document_id", "project_id", "component_ids", "classifications", "analysis_summary", "diagnostics", "facts_ref"],
            keys);
        Assert.DoesNotContain("topic", frontmatter.ToYaml(), StringComparison.Ordinal);
        Assert.DoesNotContain("domain", frontmatter.ToYaml(), StringComparison.Ordinal);
        Assert.DoesNotContain("generator", frontmatter.ToYaml(), StringComparison.Ordinal);
        Assert.DoesNotContain("timestamp", frontmatter.ToYaml(), StringComparison.Ordinal);
    }

    [Fact]
    public void Create_FactsReferenceResolvesToExactStoredDocument()
    {
        var frontmatter = Create();

        var path = Path.Combine(_root, "raw", frontmatter.FactsRef.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path));
        Assert.Contains(frontmatter.DocumentId, File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void Create_ClassificationsAndComponentIdsAreCanonicalDistinctLists()
    {
        var first = ComponentFactId.Create("worker", [ProjectId.ToFactId()]);
        var second = ComponentFactId.Create("web-api", [ProjectId.ToFactId()]);

        var frontmatter = Create([second, first, second], ["service/worker", "service/web-api", "service/worker"]);

        Assert.Equal([second.Value, first.Value], frontmatter.ComponentIds.ToArray());
        Assert.Equal(["service/web-api", "service/worker"], frontmatter.Classifications.ToArray());
    }

    [Fact]
    public void Create_AnalysisSummaryComesFromDocumentFragment()
    {
        var frontmatter = Create();

        Assert.Equal("syntactic", frontmatter.AnalysisSummary.Resolution);
        Assert.Equal(2, frontmatter.AnalysisSummary.SymbolCount);
        Assert.Equal(0, frontmatter.AnalysisSummary.RelationCount);
        Assert.Equal(0, frontmatter.AnalysisSummary.DiagnosticCount);
        Assert.Empty(frontmatter.Diagnostics);
    }

    [Fact]
    public void Schema_RequiredFieldsMatchFrontmatterRecordExactly()
    {
        var path = Path.Combine(TestPaths.RepoRoot, "schemas", "frontmatter-v2.schema.json");
        using var schema = JsonDocument.Parse(File.ReadAllText(path));
        var required = schema.RootElement.GetProperty("required").EnumerateArray()
            .Select(static item => item.GetString()!).Order(StringComparer.Ordinal).ToArray();
        var properties = typeof(FrontmatterV2).GetProperties()
            .Where(static property => property.Name != nameof(FrontmatterV2.CurrentSchemaVersion))
            .Select(static property => SnakeCase(property.Name)).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(required, properties);
        Assert.Equal(2, schema.RootElement.GetProperty("properties").GetProperty("schema_version").GetProperty("const").GetInt32());
    }

    [Fact]
    public Task Create_RepresentativeYamlMatchesApprovedSnapshot() =>
        Verifier.Verify(Create().ToYaml(), "yaml").UseDirectory("snapshots");

    private static readonly ProjectFactId ProjectId = ProjectFactId.Create("src/App.csproj");

    private FrontmatterV2 Create(
        IEnumerable<ComponentFactId>? componentIds = null,
        IEnumerable<string>? classifications = null)
    {
        var extraction = SyntaxFactExtractor.Extract(ProjectId, "src/C.cs", "class C { void Run() { } }");
        var fragment = new ValidatedFactFragment(
            [extraction.Document, .. extraction.Document.Sections, .. extraction.Symbols], []);
        var stored = new FactStore(_root).Persist(fragment);
        return FrontmatterV2.Create(fragment, stored, componentIds, classifications);
    }

    private static string[] TopLevelKeys(string yaml) => yaml.Split('\n')
        .Where(static line => line.Length > 0 && line != "---" && !char.IsWhiteSpace(line[0]))
        .Select(static line => line[..line.IndexOf(':')])
        .ToArray();

    private static string SnakeCase(string value) =>
        string.Concat(value.Select((character, index) => index > 0 && char.IsUpper(character) ? $"_{character}" : character.ToString()))
            .ToLowerInvariant();
}
