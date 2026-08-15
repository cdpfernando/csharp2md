using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Rendering;

/// <summary>
/// Additive decorator over <see cref="MarkdownRenderer"/> (AD-002): layers resolved namespace,
/// base type, and implemented interfaces onto an already-rendered document.
/// </summary>
/// <remarks>
/// The renderer knows nothing about this type. Skipping the call degrades output to
/// source-as-written rather than breaking it, which is exactly what a project that failed to
/// restore needs. Enrichment never removes or rewrites a section's source text, so the
/// span-coverage invariant survives untouched.
/// </remarks>
public static class SemanticEnricher
{
    public static RenderedDocument Enrich(RenderedDocument document, RenderContext context)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(context);

        // A null model is the expected degraded state; a model built over a different tree would
        // throw on GetDeclaredSymbol, so both are treated as "no semantics available".
        if (context.SemanticModel is not { } model || model.SyntaxTree != context.SyntaxTree)
        {
            return document;
        }

        var declarations = context.SyntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<BaseTypeDeclarationSyntax>()
            .ToDictionary(declaration => declaration.FullSpan.Start);

        string? resolvedNamespace = null;
        var sections = new List<RenderedSection>(document.Sections.Count);

        foreach (var section in document.Sections)
        {
            if (!declarations.TryGetValue(section.Span.Start, out var declaration)
                || model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
            {
                sections.Add(section);
                continue;
            }

            resolvedNamespace ??= symbol.ContainingNamespace is { IsGlobalNamespace: false } containing
                ? containing.ToDisplayString()
                : null;

            var facts = Facts(symbol);
            sections.Add(facts.Count == 0 ? section : section with { Notes = [.. section.Notes, .. facts] });
        }

        return document with
        {
            Namespace = resolvedNamespace ?? document.Namespace,
            Sections = sections,
        };
    }

    private static IReadOnlyList<string> Facts(INamedTypeSymbol symbol)
    {
        List<string>? facts = null;

        if (symbol.BaseType is { } baseType && IsResolved(baseType) && !IsCompilerSupplied(baseType))
        {
            (facts ??= []).Add($"**Base type**: `{baseType.ToDisplayString()}`");
        }

        var interfaces = symbol.Interfaces
            .Where(IsResolved)
            .Select(contract => $"`{contract.ToDisplayString()}`")
            .ToList();

        if (interfaces.Count > 0)
        {
            (facts ??= []).Add($"**Implements**: {string.Join(", ", interfaces)}");
        }

        return facts ?? (IReadOnlyList<string>)[];
    }

    // An unresolved reference surfaces as an error type symbol. Reporting it would invent a fact
    // the compilation does not actually have, so it is dropped and the source stands as written.
    private static bool IsResolved(ITypeSymbol symbol) => symbol.TypeKind != TypeKind.Error;

    // Bases the compiler supplies rather than the author writing them: noise, not information.
    private static bool IsCompilerSupplied(INamedTypeSymbol baseType) => baseType.SpecialType
        is SpecialType.System_Object
        or SpecialType.System_ValueType
        or SpecialType.System_Enum
        or SpecialType.System_Delegate
        or SpecialType.System_MulticastDelegate;
}
