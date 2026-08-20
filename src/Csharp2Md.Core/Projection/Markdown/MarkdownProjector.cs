using System.Text;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Projection.Markdown;

internal static class MarkdownProjector
{
    public static string Project(ValidatedFactFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var document = fragment.Facts.OfType<DocumentFact>().Single();
        var sections = document.Sections
            .OrderBy(static section => section.StartOffset)
            .ToArray();
        ValidatePartition(sections);

        var fence = new string('`', Math.Max(3, LongestBacktickRun(sections) + 1));
        var builder = new StringBuilder()
            .Append("# ").Append(document.RelativePath).Append("\n\n");

        AppendAnalysis(builder, document, fragment);
        foreach (var section in sections)
        {
            builder.Append("## ").Append(Title(section.SectionKind)).Append("\n\n")
                .Append(fence).Append("csharp\n")
                .Append(section.Source);
            if (!section.Source.EndsWith('\n'))
            {
                builder.Append('\n');
            }

            builder.Append(fence).Append("\n\n");
        }

        return builder.ToString();
    }

    private static void AppendAnalysis(
        StringBuilder builder,
        DocumentFact document,
        ValidatedFactFragment fragment)
    {
        var symbolResolutions = fragment.Facts.OfType<SymbolFact>()
            .Where(symbol => symbol.DocumentId == document.DocumentId)
            .Select(static symbol => symbol.Header.Resolution)
            .ToArray();
        var relationResolutions = fragment.Facts.OfType<RelationFact>()
            .Select(static relation => relation.Header.Resolution)
            .ToArray();
        if (symbolResolutions.Length == 0 && relationResolutions.Length == 0 && fragment.Diagnostics.IsEmpty)
        {
            return;
        }

        var diagnosticCounts = fragment.Diagnostics
            .GroupBy(static diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal)
            .Select(static group => (Code: group.Key, Count: group.Count()))
            .ToArray();

        builder.Append("## Analysis\n\n```yaml\n")
            .Append("resolution: ").Append(Wire(document.Header.Resolution)).Append('\n');
        AppendResolutionCounts(builder, "symbols", symbolResolutions);
        AppendResolutionCounts(builder, "relations", relationResolutions);
        AppendDiagnosticCounts(builder, diagnosticCounts);
        builder.Append("```\n\n");
    }

    private static void AppendResolutionCounts(StringBuilder builder, string key, IReadOnlyCollection<FactResolution> resolutions)
    {
        var counts = resolutions
            .GroupBy(static resolution => resolution)
            .ToDictionary(static group => group.Key, static group => group.Count());
        builder.Append(key).Append(':');
        var present = Enum.GetValues<FactResolution>().Where(counts.ContainsKey).ToArray();
        if (present.Length == 0)
        {
            builder.Append(" {}\n");
            return;
        }

        builder.Append('\n');
        foreach (var kind in present)
        {
            builder.Append("  ").Append(Wire(kind)).Append(": ").Append(counts[kind]).Append('\n');
        }
    }

    private static void AppendDiagnosticCounts(StringBuilder builder, IReadOnlyCollection<(string Code, int Count)> counts)
    {
        builder.Append("diagnostics:");
        if (counts.Count == 0)
        {
            builder.Append(" {}\n");
            return;
        }

        builder.Append('\n');
        foreach (var (code, count) in counts)
        {
            builder.Append("  ").Append(code).Append(": ").Append(count).Append('\n');
        }
    }

    private static string Wire<T>(T value) where T : struct, Enum =>
        value.ToString().ToLowerInvariant();

    private static void ValidatePartition(IReadOnlyList<SourceSectionFact> sections)
    {
        var cursor = 0;
        foreach (var section in sections)
        {
            if (section.StartOffset != cursor || section.Length != section.Source.Length)
            {
                throw new InvalidOperationException("Document source sections are not a contiguous exact partition.");
            }

            cursor += section.Length;
        }
    }

    private static int LongestBacktickRun(IEnumerable<SourceSectionFact> sections)
    {
        var longest = 0;
        var current = 0;
        foreach (var character in sections.SelectMany(static section => section.Source))
        {
            current = character == '`' ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }

    private static string Title(string sectionKind) => sectionKind switch
    {
        "preamble" => "Preamble",
        "namespace" => "Namespace",
        "namespace-end" => "End of namespace",
        "class" => "Class",
        "class-end" => "End of class",
        "struct" => "Struct",
        "struct-end" => "End of struct",
        "record" => "Record",
        "record-end" => "End of record",
        "record-struct" => "Record struct",
        "record-struct-end" => "End of record struct",
        "interface" => "Interface",
        "interface-end" => "End of interface",
        "enum" => "Enum",
        "enum-member" => "Enum member",
        "enum-end" => "End of enum",
        "top-level-statement" => "Top-level statement",
        "additional-source" => "Additional source",
        "trailing" => "Trailing source",
        _ => char.ToUpperInvariant(sectionKind[0]) + sectionKind[1..].Replace('-', ' '),
    };
}
