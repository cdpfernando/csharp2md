using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace Csharp2Md.Core.Rendering;

/// <summary>
/// One emitted Markdown section. <see cref="Span"/> is a slice of the source file, and the
/// sections of a document partition the file completely: every byte lands in exactly one
/// section, which is what makes structural rendering safe (AD-002).
/// </summary>
public sealed record RenderedSection(
    string Title,
    int Level,
    TextSpan Span,
    string Text,
    IReadOnlyList<string> Notes);

/// <summary>
/// A rendered source document. Kept as structured sections rather than a finished string so
/// the semantic enricher can decorate it additively without re-parsing Markdown.
/// </summary>
public sealed record RenderedDocument(
    string RelativePath,
    string Namespace,
    string IndexLink,
    IReadOnlyList<RenderedSection> Sections)
{
    public string ToMarkdown()
    {
        var fence = new string('`', Math.Max(3, LongestBacktickRun() + 1));
        var builder = new StringBuilder();

        builder.Append("# ").Append(RelativePath).Append("\n\n");
        builder.Append("Namespace: `").Append(Namespace).Append("` | [Index](").Append(IndexLink).Append(")\n\n");

        foreach (var section in Sections)
        {
            builder.Append('#', section.Level).Append(' ').Append(section.Title).Append("\n\n");

            foreach (var note in section.Notes)
            {
                builder.Append(note).Append("\n\n");
            }

            builder.Append(fence).Append("csharp\n").Append(section.Text);
            if (!section.Text.EndsWith('\n'))
            {
                builder.Append('\n');
            }

            builder.Append(fence).Append("\n\n");
        }

        return builder.ToString();
    }

    // A fenced block must be delimited by a longer backtick run than anything it contains,
    // otherwise source text containing ``` would break out of its own code fence.
    private int LongestBacktickRun()
    {
        var longest = 0;

        foreach (var section in Sections)
        {
            var run = 0;
            foreach (var c in section.Text)
            {
                run = c == '`' ? run + 1 : 0;
                longest = Math.Max(longest, run);
            }
        }

        return longest;
    }
}
