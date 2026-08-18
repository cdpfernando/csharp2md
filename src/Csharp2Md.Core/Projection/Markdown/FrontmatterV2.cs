using System.Text;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Storage;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Projection.Markdown;

internal sealed record FrontmatterAnalysisSummary(
    string Resolution,
    int SymbolCount,
    int RelationCount,
    int DiagnosticCount);

internal sealed record FrontmatterDiagnosticSummary(
    string Code,
    string Severity,
    int Count);

internal sealed record FrontmatterV2(
    int SchemaVersion,
    string DocumentId,
    string ProjectId,
    ImmutableArray<string> ComponentIds,
    ImmutableArray<string> Classifications,
    FrontmatterAnalysisSummary AnalysisSummary,
    ImmutableArray<FrontmatterDiagnosticSummary> Diagnostics,
    string FactsRef)
{
    public const int CurrentSchemaVersion = 2;

    public static FrontmatterV2 Create(
        ValidatedFactFragment fragment,
        StoredFactFragment stored,
        IEnumerable<ComponentFactId>? componentIds = null,
        IEnumerable<string>? classifications = null)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(stored);
        var document = fragment.Facts.OfType<DocumentFact>().Single();
        if (stored.RootFactId != document.Header.Id)
        {
            throw new ArgumentException("The facts reference does not identify this document fragment.", nameof(stored));
        }

        var symbols = fragment.Facts.OfType<SymbolFact>().Count(static symbol => symbol.Header.Kind == FactKind.Symbol);
        var relations = fragment.Facts.OfType<RelationFact>().Count();
        var diagnostics = fragment.Diagnostics
            .GroupBy(static diagnostic => new { diagnostic.Code, diagnostic.Severity })
            .Select(static group => new FrontmatterDiagnosticSummary(
                group.Key.Code,
                Wire(group.Key.Severity),
                group.Count()))
            .OrderBy(static summary => summary.Code, StringComparer.Ordinal)
            .ThenBy(static summary => summary.Severity, StringComparer.Ordinal)
            .ToImmutableArray();

        return new FrontmatterV2(
            CurrentSchemaVersion,
            document.DocumentId.Value,
            document.ProjectId.Value,
            (componentIds ?? [])
                .Select(static id => id.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray(),
            (classifications ?? [])
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToImmutableArray(),
            new FrontmatterAnalysisSummary(
                Wire(document.Header.Resolution),
                symbols,
                relations,
                fragment.Diagnostics.Length),
            diagnostics,
            stored.Reference.Value);
    }

    public string ToYaml()
    {
        var builder = new StringBuilder("---\n")
            .Append("schema_version: 2\n")
            .Append("document_id: ").Append(Quote(DocumentId)).Append('\n')
            .Append("project_id: ").Append(Quote(ProjectId)).Append('\n');
        AppendList(builder, "component_ids", ComponentIds);
        AppendList(builder, "classifications", Classifications);
        builder.Append("analysis_summary:\n")
            .Append("  resolution: ").Append(AnalysisSummary.Resolution).Append('\n')
            .Append("  symbol_count: ").Append(AnalysisSummary.SymbolCount).Append('\n')
            .Append("  relation_count: ").Append(AnalysisSummary.RelationCount).Append('\n')
            .Append("  diagnostic_count: ").Append(AnalysisSummary.DiagnosticCount).Append('\n')
            .Append("diagnostics:");
        if (Diagnostics.IsEmpty)
        {
            builder.Append(" []\n");
        }
        else
        {
            builder.Append('\n');
            foreach (var diagnostic in Diagnostics)
            {
                builder.Append("  - code: ").Append(Quote(diagnostic.Code)).Append('\n')
                    .Append("    severity: ").Append(diagnostic.Severity).Append('\n')
                    .Append("    count: ").Append(diagnostic.Count).Append('\n');
            }
        }

        return builder.Append("facts_ref: ").Append(Quote(FactsRef)).Append("\n---\n").ToString();
    }

    private static void AppendList(StringBuilder builder, string key, ImmutableArray<string> values)
    {
        builder.Append(key).Append(':');
        if (values.IsEmpty)
        {
            builder.Append(" []\n");
            return;
        }

        builder.Append('\n');
        foreach (var value in values)
        {
            builder.Append("  - ").Append(Quote(value)).Append('\n');
        }
    }

    private static string Quote(string value) =>
        '"' + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + '"';

    private static string Wire<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant();
}
