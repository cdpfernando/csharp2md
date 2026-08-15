using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Topic;

/// <summary>
/// The eleven-value <c>file_type</c> classification table (Frontmatter Schema, `file_type`
/// Derivation; WIKI-06, WIKI-13). Rules apply to the document's title type; first match in table
/// order wins, and a multi-match emits a warning naming every rule that matched. Syntax-only: every
/// check reads a declared name, the declaration kind, base-list entries, or modifiers — never the
/// semantic model, so a project that failed to restore classifies the same as one that did.
/// </summary>
internal static class FileTypeClassifier
{
    public static FileType Classify(
        bool isIndexDocument, string sourcePath, BaseTypeDeclarationSyntax? titleType, out IReadOnlyList<string> warnings)
    {
        ArgumentNullException.ThrowIfNull(sourcePath);

        // Rule 1: a generated index document. Not derived from a type at all.
        if (isIndexDocument)
        {
            warnings = [];
            return FileType.Index;
        }

        var matches = new List<(FileType FileType, string Label)>();

        // Rule 2: filename-based, independent of whether a type was resolved.
        if (Path.GetFileName(sourcePath) is "Startup.cs" or "Program.cs")
        {
            matches.Add((FileType.Configuration, "configuration"));
        }

        // Rules 3-4: declaration-kind rules — unambiguous, no name or base-type signal needed.
        switch (titleType)
        {
            case EnumDeclarationSyntax:
                matches.Add((FileType.Enum, "enum"));
                break;

            case InterfaceDeclarationSyntax:
                matches.Add((FileType.Interface, "interface"));
                break;
        }

        // Rules 5-10: base-type and name rules, all evaluated over the title type's base list and
        // its own declared name — no semantic resolution of what a base-list entry actually is.
        if (titleType is TypeDeclarationSyntax typeDeclaration)
        {
            var baseNames = BaseTypeNames(typeDeclaration);
            var ownName = typeDeclaration.Identifier.ValueText;

            if (baseNames.Any(name => name.EndsWith("Controller", StringComparison.Ordinal))
                || ownName.EndsWith("Controller", StringComparison.Ordinal))
            {
                matches.Add((FileType.Controller, "controller"));
            }

            // "implements IIntegrationEventHandler (any arity)" collapses into the suffix check:
            // BaseTypeNames already strips generic type arguments, and IIntegrationEventHandler
            // itself ends in "EventHandler".
            if (baseNames.Any(name => name.EndsWith("EventHandler", StringComparison.Ordinal)))
            {
                matches.Add((FileType.Handler, "handler"));
            }

            if (baseNames.Any(name => name is "DbContext" or "RepositoryBase"
                    || name.StartsWith("IRepository", StringComparison.Ordinal))
                || ownName.EndsWith("Repository", StringComparison.Ordinal))
            {
                matches.Add((FileType.DataAccess, "data-access"));
            }

            if (baseNames.Any(name => name is "ServiceBase" || name.StartsWith("IService", StringComparison.Ordinal))
                || ownName.EndsWith("Service", StringComparison.Ordinal))
            {
                matches.Add((FileType.Service, "service"));
            }

            if (baseNames.Any(name => name is "IOperationFilter"))
            {
                matches.Add((FileType.Filter, "filter"));
            }

            if (typeDeclaration.Modifiers.Any(SyntaxKind.StaticKeyword) && DeclaresExtensionMethod(typeDeclaration))
            {
                matches.Add((FileType.Extension, "extension"));
            }
        }

        if (matches.Count == 0)
        {
            warnings = [];
            return FileType.Class;
        }

        warnings = matches.Count > 1
            ? [$"'{sourcePath}' matched multiple file_type rules: {string.Join(", ", matches.Select(m => m.Label))}. Using the first: {matches[0].Label}."]
            : [];

        return matches[0].FileType;
    }

    private static IReadOnlyList<string> BaseTypeNames(TypeDeclarationSyntax type) =>
        type.BaseList?.Types
            .OfType<SimpleBaseTypeSyntax>()
            .Select(baseType => SimpleName(baseType.Type.ToString()))
            .ToList()
        ?? [];

    // Matches the string-based simple-name convention MessagingDetector.SimpleName already uses:
    // strip generic type arguments, then take the segment after the last namespace separator.
    private static string SimpleName(string typeName)
    {
        var genericStart = typeName.IndexOf('<');
        var withoutGenericArgs = genericStart >= 0 ? typeName[..genericStart] : typeName;
        var lastSeparator = withoutGenericArgs.LastIndexOf('.');
        return lastSeparator >= 0 ? withoutGenericArgs[(lastSeparator + 1)..] : withoutGenericArgs;
    }

    private static bool DeclaresExtensionMethod(TypeDeclarationSyntax type) =>
        type.Members.OfType<MethodDeclarationSyntax>().Any(method =>
            method.ParameterList.Parameters.Count > 0
            && method.ParameterList.Parameters[0].Modifiers.Any(SyntaxKind.ThisKeyword));
}
