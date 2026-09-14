using Csharp2Md.Analysis.Classification;
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
            .GroupBy(static symbol => ClassifierContext.SignatureKey(symbol.OwningProject, symbol.Signature), StringComparer.Ordinal)
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

            if (projects.TryGetValue(document.OwningProject.Value, out var project))
            {
                // GCPC-039/044 (partial): the project-to-document edge is justified by the document's
                // own declaration-shape evidence, not every behavioral occurrence inside it.
                var documentEvidence = EvidenceScope.For(project.Reference, document.Reference, RelationKind.Contains, observations);
                Add(context, project.Reference, document.Reference, facets, documentEvidence, classifier);
                count++;
            }

            foreach (var symbol in SymbolsDeclaredIn(context, document, symbolsBySignature, cancellationToken))
            {
                // The symbol's own declaration evidence is preferred; a symbol with none of its own
                // (e.g. a bare interface with no base list) falls back to the document's pool, still
                // scoped by EvidenceScope to exclude unrelated behavioral occurrences.
                var ownObservations = Array.FindAll(observations, observation => observation.Identity.Owner.Equals(symbol.Reference));
                var candidates = ownObservations.Length > 0 ? ownObservations : observations;
                var symbolEvidence = EvidenceScope.For(document.Reference, symbol.Reference, RelationKind.Contains, candidates);
                Add(context, document.Reference, symbol.Reference, facets, symbolEvidence, classifier);
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

        var root = AuthorizedRoot.ForSolution(context.SolutionPath);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var compilation in bound.Compilations)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                if (string.IsNullOrEmpty(tree.FilePath))
                {
                    continue;
                }

                var relative = AuthorizedRoot.ToLogicalPath(root, tree.FilePath);
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

                        var key = ClassifierContext.SignatureKey(document.OwningProject, signature.Value);
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
}
