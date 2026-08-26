using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class DataAccessDetector : IRegisteredContextDetector
{
    private static readonly NormalizedPayload EmptyPayload = NormalizedPayload.Create([]);
    private static readonly BindingDiagnostic Bound = new("bound", "bound");
    private static readonly HashSet<string> PersistenceMethodNames =
    [
        "SaveChanges",
        "SaveChangesAsync",
        "Add",
        "AddAsync",
        "FromSqlRaw",
        "FromSqlInterpolated",
        "ExecuteSqlRaw",
        "ExecuteSqlInterpolated",
    ];

    public ObservationDraft? TryObserve(BoundOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        var matches = occurrence.Node switch
        {
            MemberAccessExpressionSyntax member => IsDbSetExpression(occurrence, member),
            InvocationExpressionSyntax invocation => IsPersistenceInvocation(occurrence, invocation)
                || IsLinqOverDbSet(occurrence, invocation),
            QueryExpressionSyntax query => IsDbSetExpression(occurrence, query.FromClause.Expression),
            _ => false,
        };
        if (!matches)
        {
            return null;
        }

        return new ObservationDraft(
            occurrence.Owner,
            ObservationKind.DataAccess,
            EmptyPayload,
            ObservationMaterializer.CreateLocator(occurrence.Document, occurrence.Node),
            EvidenceMethod.Semantic,
            Bound,
            occurrence.DocumentHash);
    }

    private static bool IsPersistenceInvocation(BoundOccurrence occurrence, InvocationExpressionSyntax invocation)
    {
        if (occurrence.Model.GetSymbolInfo(invocation, occurrence.CancellationToken).Symbol is not IMethodSymbol method
            || !PersistenceMethodNames.Contains(method.Name))
        {
            return false;
        }

        var receiver = method.ReceiverType ?? method.ContainingType;
        return IsDbSet(receiver, occurrence.Compilation) || IsDbContext(receiver, occurrence.Compilation);
    }

    private static bool IsLinqOverDbSet(BoundOccurrence occurrence, InvocationExpressionSyntax invocation)
    {
        if (occurrence.Model.GetSymbolInfo(invocation, occurrence.CancellationToken).Symbol is not IMethodSymbol method
            || !IsLinqOperator(method))
        {
            return false;
        }

        return SourceBindsToDbSet(occurrence, invocation);
    }

    private static bool SourceBindsToDbSet(BoundOccurrence occurrence, ExpressionSyntax expression)
    {
        for (var current = expression; current is not null;)
        {
            if (IsDbSetExpression(occurrence, current))
            {
                return true;
            }

            current = current switch
            {
                InvocationExpressionSyntax invocation when invocation.Expression is MemberAccessExpressionSyntax member =>
                    member.Expression,
                MemberAccessExpressionSyntax member => member.Expression,
                _ => null,
            };
        }

        return false;
    }

    private static bool IsDbSetExpression(BoundOccurrence occurrence, ExpressionSyntax expression)
    {
        var type = occurrence.Model.GetTypeInfo(expression, occurrence.CancellationToken).Type;
        return IsDbSet(type, occurrence.Compilation);
    }

    private static bool IsLinqOperator(IMethodSymbol method)
    {
        var containing = method.ReducedFrom?.ContainingType ?? method.ContainingType;
        return containing.Name is "Enumerable" or "Queryable"
            && containing.ContainingNamespace is { Name: "Linq", ContainingNamespace.Name: "System" };
    }

    private static bool IsDbSet(ITypeSymbol? type, Compilation compilation)
    {
        var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
        return type is INamedTypeSymbol named
            && dbSet is not null
            && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, dbSet);
    }

    private static bool IsDbContext(ITypeSymbol? type, Compilation compilation)
    {
        var dbContext = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");
        if (dbContext is null)
        {
            return false;
        }

        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, dbContext))
            {
                return true;
            }
        }

        return false;
    }
}
