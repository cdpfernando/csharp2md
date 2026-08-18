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

        AppendAnnotations(builder, document, fragment);
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

    private static void AppendAnnotations(
        StringBuilder builder,
        DocumentFact document,
        ValidatedFactFragment fragment)
    {
        var symbols = fragment.Facts.OfType<SymbolFact>()
            .Where(symbol => symbol.DocumentId == document.DocumentId)
            .OrderBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal)
            .ToArray();
        if (symbols.Length == 0 && fragment.Diagnostics.IsEmpty)
        {
            return;
        }

        builder.Append("## Factual annotations\n\n");
        foreach (var symbol in symbols)
        {
            builder.Append("- `").Append(symbol.SymbolKind).Append("` â€” ")
                .Append(symbol.Header.Resolution.ToString().ToLowerInvariant());
            if (!symbol.Attributes.IsEmpty)
            {
                builder.Append("; attributes: ").Append(string.Join(", ", symbol.Attributes));
            }

            builder.Append('\n');
        }

        foreach (var diagnostic in fragment.Diagnostics)
        {
            builder.Append("- diagnostic `").Append(diagnostic.Code).Append("`: ")
                .Append(diagnostic.Message).Append('\n');
        }

        builder.Append('\n');
    }

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
