using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class AssignmentDetector : IRegisteredContextDetector
{
    private static readonly BindingDiagnostic Bound = new("bound", "bound");
    private static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;

    private Compilation? _compilation;
    private HashSet<ISymbol> _entities = new(SymbolEqualityComparer.Default);

    public ObservationDraft? TryObserve(BoundOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);
        if (occurrence.Node is not AssignmentExpressionSyntax assignment)
        {
            return null;
        }

        var symbol = occurrence.Model.GetSymbolInfo(assignment.Left, occurrence.CancellationToken).Symbol;
        if (symbol is not (IPropertySymbol or IFieldSymbol))
        {
            return null;
        }

        var containingType = symbol.ContainingType;
        if (containingType is null || !IsEntity(occurrence.Compilation, containingType, occurrence.CancellationToken))
        {
            return null;
        }

        var payload = NormalizedPayload.Create(
        [
            new PayloadEntry(
                "entity-type",
                StructuralLiteral.Create(LiteralRole.ProtocolName, containingType.ToDisplayString(Qualified), "entity-type")),
            new PayloadEntry("field-name", StructuralLiteral.Create(LiteralRole.FieldName, symbol.Name, "field-name")),
        ]);

        return new ObservationDraft(
            occurrence.Owner,
            ObservationKind.Assignment,
            payload,
            ObservationMaterializer.CreateLocator(occurrence.Document, assignment.Left),
            EvidenceMethod.Semantic,
            Bound,
            occurrence.DocumentHash);
    }

    private bool IsEntity(Compilation compilation, INamedTypeSymbol containingType, CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(_compilation, compilation))
        {
            _compilation = compilation;
            _entities = CollectEntities(compilation, cancellationToken);
        }

        return _entities.Contains(containingType) || _entities.Contains(containingType.OriginalDefinition);
    }

    private static HashSet<ISymbol> CollectEntities(Compilation compilation, CancellationToken cancellationToken)
    {
        var entities = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
        var dbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot(cancellationToken).DescendantNodesAndSelf())
            {
                AddFromDbSetSyntax(model, node, dbSet, entities, cancellationToken);
                AddFromDbContextDeclaration(model, node, dbSet, dbContext, entities, cancellationToken);
            }
        }

        return entities;
    }

    private static void AddFromDbSetSyntax(
        SemanticModel model,
        SyntaxNode node,
        INamedTypeSymbol? dbSet,
        HashSet<ISymbol> entities,
        CancellationToken cancellationToken)
    {
        if (node is not GenericNameSyntax generic)
        {
            return;
        }

        if (model.GetSymbolInfo(generic, cancellationToken).Symbol is INamedTypeSymbol named)
        {
            AddIfDbSetEntity(named, dbSet, entities);
        }
    }

    private static void AddFromDbContextDeclaration(
        SemanticModel model,
        SyntaxNode node,
        INamedTypeSymbol? dbSet,
        INamedTypeSymbol? dbContext,
        HashSet<ISymbol> entities,
        CancellationToken cancellationToken)
    {
        if (node is not BaseTypeDeclarationSyntax declaration || declaration.BaseList is null || dbContext is null)
        {
            return;
        }

        var derivesFromDbContext = false;
        foreach (var baseType in declaration.BaseList.Types)
        {
            if (model.GetSymbolInfo(baseType.Type, cancellationToken).Symbol is INamedTypeSymbol bound
                && SymbolEqualityComparer.Default.Equals(bound.OriginalDefinition, dbContext))
            {
                derivesFromDbContext = true;
                break;
            }
        }

        if (!derivesFromDbContext)
        {
            return;
        }

        if (model.GetDeclaredSymbol(declaration, cancellationToken) is not INamedTypeSymbol declared)
        {
            return;
        }

        foreach (var name in declared.MemberNames)
        {
            foreach (var member in declared.GetMembers(name))
            {
                var type = member switch
                {
                    IPropertySymbol property => property.Type,
                    IFieldSymbol field => field.Type,
                    _ => null,
                };
                if (type is not INamedTypeSymbol named)
                {
                    continue;
                }

                if (AddIfDbSetEntity(named, dbSet, entities))
                {
                    continue;
                }

                if (named.SpecialType is SpecialType.None)
                {
                    entities.Add(named.OriginalDefinition);
                }
            }
        }
    }

    private static bool AddIfDbSetEntity(INamedTypeSymbol named, INamedTypeSymbol? dbSet, HashSet<ISymbol> entities)
    {
        if (dbSet is null
            || !SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, dbSet)
            || named.TypeArguments is not [INamedTypeSymbol entity])
        {
            return false;
        }

        entities.Add(entity.OriginalDefinition);
        return true;
    }
}
