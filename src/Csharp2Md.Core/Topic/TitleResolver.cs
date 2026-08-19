using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Topic;

/// <summary>
/// The four-tier <c>title</c> rule (Frontmatter Schema, `title` rule; WIKI-06, WIKI-13). Walks the
/// syntax tree only, never the semantic model, so a degraded project resolves the same title a
/// healthy one does. Handles both namespace styles: <see cref="NamespaceDeclarationSyntax"/> (block)
/// and <see cref="FileScopedNamespaceDeclarationSyntax"/> — both derive from
/// <see cref="BaseNamespaceDeclarationSyntax"/>, the same base <c>MarkdownRenderer.DeclaredNamespace</c>
/// already relies on.
/// </summary>
internal static class TitleResolver
{
    public static TitleResolution Resolve(SyntaxTree tree, string sourcePath, string rootNamespace)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(sourcePath);
        ArgumentNullException.ThrowIfNull(rootNamespace);

        var root = (CompilationUnitSyntax)tree.GetRoot();

        // Top-level types only: declared directly under the compilation unit (global namespace) or
        // a namespace, never nested inside another type. DescendantNodes() walks in source order.
        var topLevelTypes = root.DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .Where(type => type.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .ToList();

        var fileName = Path.GetFileNameWithoutExtension(sourcePath);

        // Tier 1: the top-level type whose name equals the file name.
        var byFileName = topLevelTypes.FirstOrDefault(
            type => string.Equals(type.Identifier.ValueText, fileName, StringComparison.Ordinal));
        if (byFileName is not null)
        {
            return new TitleResolution(byFileName.Identifier.ValueText, byFileName, Warning: null);
        }

        // Tier 2: the first top-level type whose containing namespace starts with the project's root
        // namespace. Exists for files that declare a framework stand-in (a foreign namespace) before
        // the project's own type — PaymentsGrpcClient.cs is the shape this tier is for.
        var byRootNamespace = topLevelTypes.FirstOrDefault(
            type => ContainingNamespace(type).StartsWith(rootNamespace, StringComparison.Ordinal));
        if (byRootNamespace is not null)
        {
            return new TitleResolution(byRootNamespace.Identifier.ValueText, byRootNamespace, Warning: null);
        }

        // Tier 3: the first top-level type in source order.
        if (topLevelTypes.Count > 0)
        {
            var first = topLevelTypes[0];
            return new TitleResolution(first.Identifier.ValueText, first, Warning: null);
        }

        // Tier 4: no top-level type declared at all — fall back to the file name, with a warning.
        return new TitleResolution(
            fileName,
            Type: null,
            Warning: $"'{sourcePath}' declares no top-level type; title falls back to the file name.");
    }

    private static string ContainingNamespace(BaseTypeDeclarationSyntax type) =>
        type.Parent is BaseNamespaceDeclarationSyntax ns ? ns.Name.ToString() : string.Empty;
}

/// <summary>
/// The outcome of <see cref="TitleResolver.Resolve"/>: the resolved title, the type declaration it
/// came from (<c>null</c> only for the tier-4 fallback, where no top-level type exists), and a
/// warning — set only when tier 4 applied.
/// </summary>
internal sealed record TitleResolution(string Title, BaseTypeDeclarationSyntax? Type, string? Warning);
