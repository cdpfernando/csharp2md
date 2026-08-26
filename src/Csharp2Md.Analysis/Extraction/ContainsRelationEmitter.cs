using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DomainDocument = Csharp2Md.Domain.Facts.Document;
using DomainProject = Csharp2Md.Domain.Facts.Project;
using DomainProjectId = Csharp2Md.Domain.Identity.ProjectId;
using DomainSymbol = Csharp2Md.Domain.Facts.Symbol;

namespace Csharp2Md.Analysis.Extraction;

internal static class ContainsRelationEmitter
{
    public static int Emit(PipelineContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.BoundSolution is null || context.AnalysisVariants.IsDefaultOrEmpty)
        {
            return 0;
        }

        var snapshot = context.Accumulator.ToSnapshot();
        var observationsByPath = snapshot.Observations
            .GroupBy(static observation => observation.Locator.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var projects = snapshot.Facts.OfType<DomainProject>()
            .ToDictionary(static project => project.Id.Value, StringComparer.Ordinal);
        var symbolsBySignature = snapshot.Facts.OfType<DomainSymbol>()
            .GroupBy(static symbol => SignatureKey(symbol.OwningProject, symbol.Signature), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        var facets = FacetBinding.Create(TaxonomyTables.Default.FacetAxes, [], []);
        var classifier = ClassifierIdentity.Create("csharp2md.inventory.contains", 1);
        var count = 0;
        foreach (var document in snapshot.Facts.OfType<DomainDocument>())
        {
            if (!document.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || !observationsByPath.TryGetValue(document.RelativePath, out var observations))
            {
                continue;
            }

            var derivedFrom = EvidenceChain.Create(observations.Select(static observation => observation.Identity));
            if (projects.TryGetValue(document.OwningProject.Value, out var project))
            {
                Add(context, project.Reference, document.Reference, facets, derivedFrom, classifier);
                count++;
            }

            foreach (var symbol in SymbolsDeclaredIn(context, document, symbolsBySignature, cancellationToken))
            {
                Add(context, document.Reference, symbol.Reference, facets, derivedFrom, classifier);
                count++;
            }
        }

        return count;
    }

    private static void Add(
        PipelineContext context,
        FactReference source,
        FactReference target,
        FacetBinding facets,
        EvidenceChain derivedFrom,
        ClassifierIdentity classifier) =>
        context.Accumulator.AddRelation(
            ConfirmedRelation.Create(
                RelationKind.Contains,
                source,
                target,
                facets,
                derivedFrom,
                classifier,
                context.AnalysisVariants,
                EvidenceMethod.Syntactic));

    private static IEnumerable<DomainSymbol> SymbolsDeclaredIn(
        PipelineContext context,
        DomainDocument document,
        IReadOnlyDictionary<string, DomainSymbol> symbolsBySignature,
        CancellationToken cancellationToken)
    {
        var bound = context.BoundSolution;
        if (bound is null)
        {
            yield break;
        }

        var root = ComputeAuthorizedRoot(context.SolutionPath);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var compilation in bound.Compilations)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                if (string.IsNullOrEmpty(tree.FilePath))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(root, tree.FilePath).Replace('\\', '/');
                if (!string.Equals(relative, document.RelativePath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var model = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot(cancellationToken).DescendantNodesAndSelf())
                {
                    foreach (var symbol in DeclaredOn(model, node, cancellationToken))
                    {
                        var signature = SymbolFactEmitter.TrySignature(symbol);
                        if (signature is null)
                        {
                            continue;
                        }

                        var key = SignatureKey(document.OwningProject, signature.Value);
                        if (symbolsBySignature.TryGetValue(key, out var fact) && seen.Add(fact.Reference.Id.Value))
                        {
                            yield return fact;
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<ISymbol> DeclaredOn(SemanticModel model, SyntaxNode node, CancellationToken cancellationToken)
    {
        var declared = model.GetDeclaredSymbol(node, cancellationToken);
        if (declared is not null)
        {
            yield return declared;
        }

        if (node is TypeDeclarationSyntax { ParameterList: not null } && declared is INamedTypeSymbol named)
        {
            foreach (var constructor in named.InstanceConstructors)
            {
                if (!constructor.IsImplicitlyDeclared)
                {
                    yield return constructor;
                }
            }
        }
    }

    private static string SignatureKey(DomainProjectId projectId, CanonicalSymbolSignature signature) =>
        projectId.Value + "\u001f" + signature.Value;

    private static string ComputeAuthorizedRoot(string solutionPath)
    {
        var listed = SolutionFileReader.ReadProjectPaths(solutionPath);
        var solutionDirectory = Path.GetDirectoryName(Path.GetFullPath(solutionPath))
            ?? throw new InvalidOperationException($"'{solutionPath}' has no containing directory.");
        var existing = listed
            .Select(listedPath => Path.GetFullPath(Path.Combine(solutionDirectory, listedPath)))
            .Where(File.Exists);
        return AuthorizedRoot.Compute(solutionPath, existing);
    }
}
