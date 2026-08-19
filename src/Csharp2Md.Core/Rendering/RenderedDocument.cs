using System.Text;
using Csharp2Md.Core.Topic;
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
    /// <summary>
    /// The "Dependências detectadas" section (P2-10), attached by
    /// <see cref="DependencySectionRenderer"/>. It sits outside <see cref="Sections"/> on purpose:
    /// it maps to no source bytes, and a zero-span entry in that list would break the span-coverage
    /// invariant. <c>null</c> when the document has no detected dependencies.
    /// </summary>
    public string? DependencySection { get; init; }

    /// <summary>
    /// The YAML frontmatter block <see cref="ToMarkdown"/> prepends above the heading (WIKI-05,
    /// WIKI-08). Same reasoning as <see cref="DependencySection"/>: it maps to no source bytes, so a
    /// slot inside <see cref="Sections"/> would break the span-coverage invariant. <c>null</c> renders
    /// exactly as before frontmatter existed.
    /// </summary>
    public Frontmatter? Frontmatter { get; init; }

    public string ToMarkdown()
    {
        var fence = new string('`', Math.Max(3, LongestBacktickRun() + 1));
        var builder = new StringBuilder();

        if (Frontmatter is { } frontmatter)
        {
            // A blank line separates the block from the heading, matching the schema's fenced shape.
            builder.Append(FrontmatterYaml.Render(frontmatter)).Append('\n');
        }

        builder.Append("# ").Append(RelativePath).Append("\n\n");
        builder.Append("Namespace: `").Append(Namespace).Append("` | [Index](").Append(IndexLink).Append(")\n\n");

        // design.md's document structure: heading -> namespace + backlink -> dependencies -> source.
        if (DependencySection is { } dependencies)
        {
            builder.Append(dependencies).Append('\n');
        }

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
