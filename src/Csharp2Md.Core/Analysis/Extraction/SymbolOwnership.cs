using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Analysis.Extraction;

// Carries a directly-referenced project's own Compilation alongside its identity, so a symbol's true
// owner can be resolved by exact SyntaxTree identity instead of being attributed to whichever project
// happened to observe it.
internal sealed record ReferencedProjectCompilation(ProjectIdentity Project, Compilation Compilation);

// Resolves the project that actually owns a symbol's declaration, by exact SyntaxTree identity - never
// by path or name matching. A tree belonging to the root's own Compilation owns to the root; a tree
// belonging to a directly-referenced project's own Compilation owns to that project. Anything else (a
// symbol declared through a deeper transitive reference the root does not list directly, or a symbol
// with no in-source declaration at all) is unresolved.
//
// DEP-01 requires proven membership: a shared symbol declared in a directly-referenced project must be
// attributed to that project, not to every project that happens to reference it - otherwise Component/
// DeploymentUnit-scope lifting crosses each citing project against every other project sharing the
// symbol instead of against its true owner.
internal static class SymbolOwnership
{
    public static ProjectIdentity? Resolve(
        ISymbol symbol,
        ProjectIdentity project,
        Compilation compilation,
        ImmutableArray<ReferencedProjectCompilation> referencedCompilations)
    {
        var tree = symbol.Locations.FirstOrDefault(static location => location.IsInSource)?.SourceTree;
        if (tree is null)
        {
            return null;
        }

        if (compilation.SyntaxTrees.Contains(tree))
        {
            return project;
        }

        foreach (var referenced in referencedCompilations)
        {
            if (referenced.Compilation.SyntaxTrees.Contains(tree))
            {
                return referenced.Project;
            }
        }

        return null;
    }

    // Builds a locator naming the symbol's own declaration site under its resolved owner, rather than the
    // citing project's call site - matching how a ProjectReference target already names itself (see
    // PackageBuilder.cs). Keeping the locator's project consistent with the occurrence's owner matters:
    // Document-scope membership is derived from the locator's path, so a mismatched locator would still
    // attribute the wrong document even after the occurrence's own Project field is corrected.
    public static LogicalLocator DeclarationLocator(ISymbol symbol, ProjectIdentity owner)
    {
        var location = symbol.Locations.First(static candidate => candidate.IsInSource);
        var lineSpan = location.GetLineSpan();
        var span = new SourceSpan(
            lineSpan.StartLinePosition.Line + 1,
            lineSpan.StartLinePosition.Character + 1,
            lineSpan.EndLinePosition.Line + 1,
            lineSpan.EndLinePosition.Character + 1);
        var directory = Path.GetDirectoryName(owner.LogicalRelativePath)?.Replace('\\', '/') ?? string.Empty;
        var fileName = Path.GetFileName(location.SourceTree?.FilePath);
        var path = string.IsNullOrEmpty(fileName)
            ? owner.LogicalRelativePath
            : string.IsNullOrEmpty(directory) ? fileName : directory + "/" + fileName;
        return new LogicalLocator(path, span, owner);
    }
}
