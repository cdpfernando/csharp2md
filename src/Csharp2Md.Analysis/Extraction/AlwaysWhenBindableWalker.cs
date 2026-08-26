using Csharp2Md.Analysis.Inventory;
using Csharp2Md.Analysis.Pipeline;
using Csharp2Md.Analysis.Semantics;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using DomainDocument = Csharp2Md.Domain.Facts.Document;
using DomainProjectId = Csharp2Md.Domain.Identity.ProjectId;
using DomainSymbol = Csharp2Md.Domain.Facts.Symbol;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class AlwaysWhenBindableWalker : CSharpSyntaxWalker
{
    private static readonly NormalizedPayload EmptyPayload = NormalizedPayload.Create([]);
    private static readonly BindingDiagnostic Bound = new("bound", "bound");

    private readonly SemanticModel _model;
    private readonly DomainDocument _document;
    private readonly DocumentHash _documentHash;
    private readonly IReadOnlyDictionary<string, FactReference> _symbolsBySignature;
    private readonly FactReference _fallbackOwner;
    private readonly List<ObservationDraft> _drafts = [];
    private readonly CancellationToken _cancellationToken;

    private AlwaysWhenBindableWalker(
        SemanticModel model,
        DomainDocument document,
        DocumentHash documentHash,
        IReadOnlyDictionary<string, FactReference> symbolsBySignature,
        FactReference fallbackOwner,
        CancellationToken cancellationToken)
    {
        _model = model;
        _document = document;
        _documentHash = documentHash;
        _symbolsBySignature = symbolsBySignature;
        _fallbackOwner = fallbackOwner;
        _cancellationToken = cancellationToken;
    }

    internal static ImmutableArray<ObservationDraft> Collect(
        PipelineContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var bound = context.BoundSolution
            ?? throw new InvalidOperationException("Always-when-bindable extraction requires a bound solution.");

        var snapshot = context.Accumulator.ToSnapshot();
        var csharpDocuments = context.CSharpDocuments;
        if (csharpDocuments.IsDefaultOrEmpty)
        {
            csharpDocuments = [.. snapshot.Facts.OfType<DomainDocument>()
                .Where(static document => document.RelativePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))];
        }

        var documentsByRelativePath = csharpDocuments
            .GroupBy(static document => document.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var symbols = snapshot.Facts.OfType<DomainSymbol>().ToArray();
        var symbolsBySignature = symbols
            .GroupBy(static symbol => SignatureKey(symbol.OwningProject, symbol.Signature), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First().Reference, StringComparer.Ordinal);
        var fallbackByProject = symbols
            .GroupBy(static symbol => symbol.OwningProject.Value, StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(static symbol => symbol.Signature.Value, StringComparer.Ordinal).First().Reference,
                StringComparer.Ordinal);

        var root = ComputeAuthorizedRoot(context.SolutionPath);
        var drafts = ImmutableArray.CreateBuilder<ObservationDraft>();
        foreach (var compilation in bound.Compilations)
        {
            foreach (var tree in compilation.SyntaxTrees)
            {
                if (string.IsNullOrEmpty(tree.FilePath))
                {
                    continue;
                }

                var relative = Path.GetRelativePath(root, tree.FilePath).Replace('\\', '/');
                if (!documentsByRelativePath.TryGetValue(relative, out var document))
                {
                    continue;
                }

                if (!fallbackByProject.TryGetValue(document.OwningProject.Value, out var fallbackOwner))
                {
                    continue;
                }

                var model = compilation.GetSemanticModel(tree);
                var walker = new AlwaysWhenBindableWalker(
                    model,
                    document,
                    HashFileBytes(tree.FilePath),
                    symbolsBySignature,
                    fallbackOwner,
                    cancellationToken);
                walker.Visit(tree.GetRoot(cancellationToken));
                drafts.AddRange(walker._drafts);
            }
        }

        return drafts.ToImmutable();
    }

    internal static void ExtractInto(PipelineContext context, CancellationToken cancellationToken)
    {
        foreach (var observation in OccurrenceOrdinalAssigner.Assign(Collect(context, cancellationToken)))
        {
            context.Accumulator.AddObservation(observation);
        }
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        TryEmitBindable(node, ObservationKind.Invocation);
        base.VisitInvocationExpression(node);
    }

    public override void VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        TryEmitBindable(node, ObservationKind.ObjectCreation);
        base.VisitObjectCreationExpression(node);
    }

    public override void VisitImplicitObjectCreationExpression(ImplicitObjectCreationExpressionSyntax node)
    {
        TryEmitBindable(node, ObservationKind.ObjectCreation);
        base.VisitImplicitObjectCreationExpression(node);
    }

    public override void VisitAnonymousObjectCreationExpression(AnonymousObjectCreationExpressionSyntax node)
    {
        TryEmitBindable(node, ObservationKind.ObjectCreation);
        base.VisitAnonymousObjectCreationExpression(node);
    }

    public override void VisitAttribute(AttributeSyntax node)
    {
        TryEmitBindable(node, ObservationKind.AttributeUsage);
        base.VisitAttribute(node);
    }

    public override void VisitBaseList(BaseListSyntax node)
    {
        foreach (var baseType in node.Types)
        {
            TryEmitBindable(baseType.Type, ObservationKind.BaseType);
        }

        base.VisitBaseList(node);
    }

    public override void VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (node.Parent is not (QualifiedNameSyntax or AliasQualifiedNameSyntax) && !node.IsVar)
        {
            TryEmitTypeUsage(node);
        }

        base.VisitIdentifierName(node);
    }

    public override void VisitGenericName(GenericNameSyntax node)
    {
        TryEmitTypeUsage(node);
        base.VisitGenericName(node);
    }

    public override void VisitQualifiedName(QualifiedNameSyntax node)
    {
        TryEmitTypeUsage(node);
        base.VisitQualifiedName(node);
    }

    public override void VisitPredefinedType(PredefinedTypeSyntax node)
    {
        TryEmitTypeUsage(node);
        base.VisitPredefinedType(node);
    }

    public override void VisitAliasQualifiedName(AliasQualifiedNameSyntax node)
    {
        TryEmitTypeUsage(node);
        base.VisitAliasQualifiedName(node);
    }

    private void TryEmitTypeUsage(ExpressionSyntax node) => TryEmitBindable(node, ObservationKind.TypeUsage, requireType: true);

    private void TryEmitBindable(SyntaxNode node, ObservationKind kind, bool requireType = false)
    {
        var info = _model.GetSymbolInfo(node, _cancellationToken);
        if (info.Symbol is null)
        {
            return;
        }

        if (requireType && info.Symbol is not ITypeSymbol)
        {
            return;
        }

        var owner = ResolveOwner(node);
        if (owner is null)
        {
            return;
        }

        _drafts.Add(
            new ObservationDraft(
                owner.Value,
                kind,
                EmptyPayload,
                CreateLocator(node),
                EvidenceMethod.Semantic,
                Bound,
                _documentHash));
    }

    private FactReference? ResolveOwner(SyntaxNode node)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            var declared = _model.GetDeclaredSymbol(current, _cancellationToken);
            if (declared is null)
            {
                continue;
            }

            var mapped = MapInventoried(declared, _document.OwningProject);
            if (mapped is not null)
            {
                return mapped;
            }
        }

        return _fallbackOwner;
    }

    private FactReference? MapInventoried(ISymbol symbol, DomainProjectId projectId)
    {
        for (var current = symbol; current is not null; current = current.ContainingSymbol)
        {
            var signature = SymbolFactEmitter.TrySignature(current);
            if (signature is null)
            {
                continue;
            }

            if (_symbolsBySignature.TryGetValue(SignatureKey(projectId, signature.Value), out var reference))
            {
                return reference;
            }
        }

        return null;
    }

    private EvidenceLocator CreateLocator(SyntaxNode node)
    {
        var lineSpan = node.SyntaxTree.GetLineSpan(node.Span);
        var start = lineSpan.StartLinePosition;
        var end = lineSpan.EndLinePosition;
        var span = new SourceSpan(
            start.Line + 1,
            start.Character + 1,
            Math.Max(end.Line + 1, start.Line + 1),
            Math.Max(end.Character + 1, 1));
        return new EvidenceLocator(
            Domain.Literals.DocumentId.Create(_document.Reference.Id.Value),
            _document.RelativePath,
            span);
    }

    private static DocumentHash HashFileBytes(string absolutePath)
    {
        var digest = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(absolutePath)));
        return DocumentHash.Create(digest);
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
