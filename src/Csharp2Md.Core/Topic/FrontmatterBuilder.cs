using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Topic;

/// <summary>
/// Composes <see cref="TitleResolver"/>, <see cref="FileTypeClassifier"/>, and
/// <see cref="TagDeriver"/> into a <see cref="Frontmatter"/>, collecting every warning along the way
/// (WIKI-06, WIKI-13). Syntax tree in, model out — <see cref="Build"/> never accepts a
/// <c>SemanticModel</c>, so a document from a degraded project derives exactly what the same
/// document derives in a healthy one.
/// </summary>
internal static class FrontmatterBuilder
{
    public static Frontmatter Build(
        SyntaxTree tree,
        string sourcePath,
        string rootNamespace,
        TopicOptions options,
        out IReadOnlyList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(rootNamespace);
        ArgumentNullException.ThrowIfNull(options);

        // source_path is forward-slash separated on every platform, regardless of the separator the
        // caller's path used.
        var normalizedSourcePath = sourcePath.Replace('\\', '/');

        var title = TitleResolver.Resolve(tree, normalizedSourcePath, rootNamespace);
        var fileType = FileTypeClassifier.Classify(
            isIndexDocument: false, normalizedSourcePath, title.Type, out var fileTypeWarnings);
        var tags = TagDeriver.Derive(tree, title.Type);

        var collectedWarnings = new List<string>();
        if (title.Warning is { } titleWarning)
        {
            collectedWarnings.Add(titleWarning);
        }

        collectedWarnings.AddRange(fileTypeWarnings);

        // design.md Risks: a syntax error in a type header can cost the parser the base list (or
        // corrupt a base-type reference) while the type's own identifier survives intact, silently
        // downgrading a classification that was recoverable to `class` — with no crash and no other
        // signal. file_type never reaches `class` through a dedicated rule, only because every other
        // rule failed to match, so flagging every such fallback (title resolved, but no rule fired)
        // is the only syntax-only way to make that downgrade visible rather than silent. This is
        // broader than "detectably malformed" — an ordinary, correctly-parsed class with no matching
        // vocabulary in its name or base list also gets the warning — deliberately, since the tool
        // cannot tell the two apart without a semantic model it is not allowed to consult.
        if (fileType is FileType.Class && fileTypeWarnings.Count == 0 && title.Type is not null)
        {
            collectedWarnings.Add(
                $"'{normalizedSourcePath}' classified as 'class': no file_type rule matched its declared name or base list.");
        }

        warnings = collectedWarnings;

        return new Frontmatter(
            Title: title.Title,
            SourceKind: SourceKind.CodebaseFile,
            SourcePath: normalizedSourcePath,
            Domain: options.Domain,
            Topic: options.Topic,
            FileType: fileType,
            Tags: tags);
    }
}
