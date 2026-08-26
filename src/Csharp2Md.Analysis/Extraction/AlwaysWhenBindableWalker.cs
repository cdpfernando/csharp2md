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
    private const string BoundCode = "bound";
    private const string BoundSignaturePrefix = "bound::";

    private static readonly NormalizedPayload EmptyPayload = NormalizedPayload.Create([]);
    private static readonly BindingDiagnostic Bound = new(BoundCode, BoundCode);

    private readonly SemanticModel _model;
    private readonly DomainDocument _document;
    private readonly DocumentHash _documentHash;
    private readonly IReadOnlyDictionary<string, FactReference> _symbolsBySignature;
    private readonly FactReference _fallbackOwner;
    private readonly IReadOnlyList<IRegisteredContextDetector> _detectors;
    private readonly List<ObservationDraft> _drafts = [];
    private readonly CancellationToken _cancellationToken;

    private AlwaysWhenBindableWalker(
        SemanticModel model,
        DomainDocument document,
        DocumentHash documentHash,
        IReadOnlyDictionary<string, FactReference> symbolsBySignature,
        FactReference fallbackOwner,
        IReadOnlyList<IRegisteredContextDetector> detectors,
        CancellationToken cancellationToken)
    {
        _model = model;
        _document = document;
        _documentHash = documentHash;
        _symbolsBySignature = symbolsBySignature;
        _fallbackOwner = fallbackOwner;
        _detectors = detectors;
        _cancellationToken = cancellationToken;
    }

    internal static ImmutableArray<ImmutableArray<ObservationDraft>> CollectByCompilation(
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
        var batches = ImmutableArray.CreateBuilder<ImmutableArray<ObservationDraft>>();
        foreach (var compilation in bound.Compilations)
        {
            IRegisteredContextDetector[] detectors =
            [
                new AssignmentDetector(),
                new ConfigurationDetector(),
                new RouteDeclarationDetector(),
                new MessageOperationDetector(),
                new DataAccessDetector(),
            ];
            var trees = compilation.SyntaxTrees.ToArray();
            var batch = ImmutableArray.CreateBuilder<ObservationDraft>();
            foreach (var tree in trees)
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
                    ObservationMaterializer.HashFileBytes(tree.FilePath),
                    symbolsBySignature,
                    fallbackOwner,
                    detectors,
                    cancellationToken);
                walker.Visit(tree.GetRoot(cancellationToken));
                batch.AddRange(walker._drafts);
            }

            batches.Add(batch.ToImmutable());
        }

        return batches.ToImmutable();
    }

    internal static ImmutableArray<ObservationDraft> Collect(
        PipelineContext context,
        CancellationToken cancellationToken) =>
        [.. CollectByCompilation(context, cancellationToken).SelectMany(static batch => batch)];

    internal static void ExtractInto(PipelineContext context, CancellationToken cancellationToken)
    {
        foreach (var batch in CollectByCompilation(context, cancellationToken))
        {
            var redacted = batch.Select(draft => ObservationMaterializer.Redact(draft, context.Accumulator));
            foreach (var observation in OccurrenceOrdinalAssigner.Assign(redacted))
            {
                context.Accumulator.AddObservation(observation);
            }
        }
    }

    public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
    {
        TryEmitRegistered(node);
        base.VisitAssignmentExpression(node);
    }

    public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
    {
        TryEmitRegistered(node);
        base.VisitElementAccessExpression(node);
    }

    public override void VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        TryEmitRegistered(node);
        base.VisitMemberAccessExpression(node);
    }

    public override void VisitQueryExpression(QueryExpressionSyntax node)
    {
        TryEmitRegistered(node);
        base.VisitQueryExpression(node);
    }

    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        TryEmitBindable(node, ObservationKind.Invocation);
        TryEmitRegistered(node);
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
        TryEmitRegistered(node);
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

    private void TryEmitRegistered(SyntaxNode node)
    {
        var owner = ResolveOwner(node);
        if (owner is null)
        {
            return;
        }

        var occurrence = new BoundOccurrence(
            node,
            _model,
            _document,
            _documentHash,
            owner.Value,
            _cancellationToken);
        foreach (var detector in _detectors)
        {
            var draft = detector.TryObserve(occurrence);
            if (draft is not null)
            {
                _drafts.Add(draft);
            }
        }
    }

    private void TryEmitTypeUsage(ExpressionSyntax node) => TryEmitBindable(node, ObservationKind.TypeUsage, requireType: true);

    private void TryEmitBindable(SyntaxNode node, ObservationKind kind, bool requireType = false)
    {
        var info = _model.GetSymbolInfo(node, _cancellationToken);
        var boundSymbol = info.Symbol;
        EvidenceMethod evidenceMethod;
        BindingDiagnostic diagnostic;
        if (boundSymbol is not null && (!requireType || boundSymbol is ITypeSymbol))
        {
            evidenceMethod = EvidenceMethod.Semantic;
            diagnostic = BoundDiagnostic(kind, boundSymbol);
        }
        else if (boundSymbol is null && kind is not ObservationKind.TypeUsage)
        {
            evidenceMethod = EvidenceMethod.Syntactic;
            diagnostic = new BindingDiagnostic("unbound", "The occurrence did not bind.");
        }
        else
        {
            return;
        }

        var owner = ResolveOwner(node);
        if (owner is null)
        {
            return;
        }

        var payload = kind is ObservationKind.Invocation && node is InvocationExpressionSyntax invocation
            ? boundSymbol is IMethodSymbol invoked
                ? InvocationPayload(invoked, invocation)
                : InvocationPayloadFromSyntax(invocation)
            : EmptyPayload;

        _drafts.Add(
            new ObservationDraft(
                owner.Value,
                kind,
                payload,
                ObservationMaterializer.CreateLocator(_document, node),
                evidenceMethod,
                diagnostic,
                _documentHash));
    }

    internal static string? TryExtractTargetSignature(string diagnosticMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticMessage);
        if (!diagnosticMessage.StartsWith(BoundSignaturePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var signature = diagnosticMessage[BoundSignaturePrefix.Length..];
        return signature.Length > 0 ? signature : null;
    }

    private static BindingDiagnostic BoundDiagnostic(ObservationKind kind, ISymbol boundSymbol)
    {
        if (kind is ObservationKind.Invocation or ObservationKind.ObjectCreation
            && boundSymbol is IMethodSymbol
            && SymbolFactEmitter.TrySignature(boundSymbol) is { } signature)
        {
            return new BindingDiagnostic(BoundCode, BoundSignaturePrefix + signature.Value);
        }

        return Bound;
    }

    private static readonly HashSet<string> HttpInvocationNames =
    [
        "PostAsJsonAsync",
        "GetAsync",
        "PutAsJsonAsync",
        "DeleteAsync",
        "SendAsync",
    ];

    private static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;

    private NormalizedPayload InvocationPayload(IMethodSymbol method, InvocationExpressionSyntax invocation)
    {
        var entries = new List<PayloadEntry>
        {
            new("method-name", StructuralLiteral.Create(LiteralRole.ProtocolName, method.Name, "method-name")),
        };
        var receiver = method.ReceiverType ?? method.ContainingType;
        if (receiver is not null)
        {
            entries.Add(
                new PayloadEntry(
                    "target-type",
                    StructuralLiteral.Create(LiteralRole.ProtocolName, receiver.ToDisplayString(Qualified), "target-type")));
        }

        var literal = TryFirstStringLiteral(invocation);
        if (literal is not null)
        {
            if (string.Equals(method.Name, "CreateClient", StringComparison.Ordinal))
            {
                entries.Add(
                    new PayloadEntry(
                        "client-name",
                        StructuralLiteral.Create(LiteralRole.ClientName, literal, "client-name")));
            }
            else if (HttpInvocationNames.Contains(method.Name))
            {
                entries.Add(
                    new PayloadEntry(
                        "route",
                        StructuralLiteral.Create(LiteralRole.Route, literal, "route")));
            }
        }

        entries.AddRange(EfMappingPayload.For(_model, method, invocation, _cancellationToken));

        return NormalizedPayload.Create(entries);
    }

    private static NormalizedPayload InvocationPayloadFromSyntax(InvocationExpressionSyntax invocation)
    {
        var methodName = TryInvocationMethodName(invocation);
        if (methodName is null)
        {
            return EmptyPayload;
        }

        var entries = new List<PayloadEntry>
        {
            new("method-name", StructuralLiteral.Create(LiteralRole.ProtocolName, methodName, "method-name")),
        };
        var literal = TryFirstStringLiteral(invocation);
        if (literal is not null)
        {
            if (string.Equals(methodName, "CreateClient", StringComparison.Ordinal))
            {
                entries.Add(
                    new PayloadEntry(
                        "client-name",
                        StructuralLiteral.Create(LiteralRole.ClientName, literal, "client-name")));
            }
            else if (HttpInvocationNames.Contains(methodName))
            {
                entries.Add(
                    new PayloadEntry(
                        "route",
                        StructuralLiteral.Create(LiteralRole.Route, literal, "route")));
            }
        }

        return NormalizedPayload.Create(entries);
    }

    private static string? TryInvocationMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            _ => null,
        };

    private static string? TryFirstStringLiteral(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax literal
                && literal.Token.IsKind(SyntaxKind.StringLiteralToken)
                && !string.IsNullOrWhiteSpace(literal.Token.ValueText))
            {
                return literal.Token.ValueText;
            }
        }

        return null;
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
