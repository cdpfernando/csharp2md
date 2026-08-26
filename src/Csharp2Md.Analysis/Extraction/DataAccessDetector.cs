using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Analysis.Extraction;

internal sealed class DataAccessDetector : IRegisteredContextDetector
{
    private static readonly BindingDiagnostic Bound = new("bound", "bound");
    private static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;
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

    private static readonly HashSet<string> RawSqlMethodNames =
    [
        "FromSqlRaw",
        "FromSqlInterpolated",
        "ExecuteSqlRaw",
        "ExecuteSqlInterpolated",
    ];

    /// <summary>What one recognized data-access occurrence contributes to the payload.</summary>
    private readonly record struct DataAccessMatch(
        DataOperationKind Operation,
        string? EntityTypeFqn,
        string? ContextTypeFqn,
        ImmutableArray<string> FieldNames,
        SqlStatementFacts? SqlFacts = null);

    public ObservationDraft? TryObserve(BoundOccurrence occurrence)
    {
        ArgumentNullException.ThrowIfNull(occurrence);

        var match = occurrence.Node switch
        {
            MemberAccessExpressionSyntax member => TryDbSetMemberAccess(occurrence, member),
            InvocationExpressionSyntax invocation =>
                TryPersistenceInvocation(occurrence, invocation) ?? TryLinqOverDbSet(occurrence, invocation),
            QueryExpressionSyntax query => TryDbSetMemberAccess(occurrence, query.FromClause.Expression),
            _ => null,
        };
        if (match is not { } resolved)
        {
            return null;
        }

        return new ObservationDraft(
            occurrence.Owner,
            ObservationKind.DataAccess,
            BuildPayload(resolved),
            ObservationMaterializer.CreateLocator(occurrence.Document, occurrence.Node),
            EvidenceMethod.Semantic,
            Bound,
            occurrence.DocumentHash);
    }

    private static DataAccessMatch? TryDbSetMemberAccess(BoundOccurrence occurrence, ExpressionSyntax expression)
    {
        var type = occurrence.Model.GetTypeInfo(expression, occurrence.CancellationToken).Type;
        if (!TryDbSetEntityType(type, occurrence.Compilation, out var entityType))
        {
            return null;
        }

        return new DataAccessMatch(DataOperationKind.Unknown, Qualify(entityType), null, ImmutableArray<string>.Empty);
    }

    private static DataAccessMatch? TryPersistenceInvocation(BoundOccurrence occurrence, InvocationExpressionSyntax invocation)
    {
        if (occurrence.Model.GetSymbolInfo(invocation, occurrence.CancellationToken).Symbol is not IMethodSymbol method
            || !PersistenceMethodNames.Contains(method.Name))
        {
            return null;
        }

        var sqlFacts = RawSqlMethodNames.Contains(method.Name) ? TryReadSqlFacts(invocation) : null;
        var receiver = ResolveReceiverType(occurrence, invocation, method);
        if (TryDbSetEntityType(receiver, occurrence.Compilation, out var entityType))
        {
            var operation = ResolveOperation(method.Name, sqlFacts);
            return new DataAccessMatch(operation, Qualify(entityType), null, ImmutableArray<string>.Empty, sqlFacts);
        }

        if (IsDbContext(receiver, occurrence.Compilation))
        {
            var operation = ResolveOperation(method.Name, sqlFacts);
            return new DataAccessMatch(operation, null, Qualify(receiver), ImmutableArray<string>.Empty, sqlFacts);
        }

        return null;
    }

    private static DataOperationKind ResolveOperation(string methodName, SqlStatementFacts? sqlFacts)
    {
        if (sqlFacts is { } facts)
        {
            return facts.Operation;
        }

        return methodName is "Add" or "AddAsync" ? DataOperationKind.Insert : DataOperationKind.Unknown;
    }

    /// <summary>
    /// Reads the invocation's raw-SQL statement into <see cref="SqlStatementFacts"/> when the
    /// argument is a constant string literal or a fully-literal interpolated string (no holes). A
    /// non-constant statement - including any statement with an actual interpolation hole - yields
    /// <see langword="null"/>, so the statement text never becomes payload evidence (PK-04).
    /// </summary>
    private static SqlStatementFacts? TryReadSqlFacts(InvocationExpressionSyntax invocation)
    {
        var statementText = TryConstantStatementText(invocation);
        if (statementText is null)
        {
            return null;
        }

        return SqlStatementReader.TryRead(statementText, out var facts) ? facts : null;
    }

    private static string? TryConstantStatementText(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            switch (argument.Expression)
            {
                case LiteralExpressionSyntax literal when literal.Token.IsKind(SyntaxKind.StringLiteralToken):
                    return literal.Token.ValueText;
                case InterpolatedStringExpressionSyntax interpolated
                    when interpolated.Contents.All(static content => content is InterpolatedStringTextSyntax):
                    return string.Concat(
                        interpolated.Contents
                            .OfType<InterpolatedStringTextSyntax>()
                            .Select(static text => text.TextToken.ValueText));
            }
        }

        return null;
    }

    /// <summary>
    /// The receiver expression's own compile-time type (e.g. the concrete <c>OrderDbContext</c> of
    /// <c>_context</c>), not the declaring method's <see cref="IMethodSymbol.ContainingType"/> -
    /// SaveChanges is declared on <c>DbContext</c>, so relying on ContainingType would report the base
    /// type even when the receiver is a derived context.
    /// </summary>
    private static ITypeSymbol? ResolveReceiverType(BoundOccurrence occurrence, InvocationExpressionSyntax invocation, IMethodSymbol method)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member)
        {
            var receiverType = occurrence.Model.GetTypeInfo(member.Expression, occurrence.CancellationToken).Type;
            if (receiverType is not null)
            {
                return receiverType;
            }
        }

        return method.ReceiverType ?? method.ContainingType;
    }

    private static DataAccessMatch? TryLinqOverDbSet(BoundOccurrence occurrence, InvocationExpressionSyntax invocation)
    {
        if (occurrence.Model.GetSymbolInfo(invocation, occurrence.CancellationToken).Symbol is not IMethodSymbol method
            || !IsLinqOperator(method))
        {
            return null;
        }

        if (!TryResolveDbSetFromChain(occurrence, invocation, out var entityType))
        {
            return null;
        }

        var fieldNames = ResolveFieldNames(occurrence, invocation, entityType!);
        return new DataAccessMatch(DataOperationKind.Read, Qualify(entityType), null, fieldNames);
    }

    private static bool TryResolveDbSetFromChain(BoundOccurrence occurrence, ExpressionSyntax expression, out ITypeSymbol? entityType)
    {
        for (var current = expression; current is not null;)
        {
            var type = occurrence.Model.GetTypeInfo(current, occurrence.CancellationToken).Type;
            if (TryDbSetEntityType(type, occurrence.Compilation, out var resolved))
            {
                entityType = resolved;
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

        entityType = null;
        return false;
    }

    private static ImmutableArray<string> ResolveFieldNames(
        BoundOccurrence occurrence,
        InvocationExpressionSyntax invocation,
        ITypeSymbol entityType)
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            var (parameter, body) = argument.Expression switch
            {
                SimpleLambdaExpressionSyntax simple => (simple.Parameter, (SyntaxNode)simple.Body),
                ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters: [var single] } parenthesized =>
                    (single, parenthesized.Body),
                _ => (null, null),
            };
            if (parameter is null || body is null)
            {
                continue;
            }

            var parameterSymbol = occurrence.Model.GetDeclaredSymbol(parameter, occurrence.CancellationToken);
            if (parameterSymbol is null)
            {
                continue;
            }

            foreach (var member in body.DescendantNodesAndSelf().OfType<MemberAccessExpressionSyntax>())
            {
                if (member.Expression is not IdentifierNameSyntax identifier
                    || !SymbolEqualityComparer.Default.Equals(
                        occurrence.Model.GetSymbolInfo(identifier, occurrence.CancellationToken).Symbol,
                        parameterSymbol))
                {
                    continue;
                }

                if (occurrence.Model.GetSymbolInfo(member, occurrence.CancellationToken).Symbol is IPropertySymbol or IFieldSymbol)
                {
                    names.Add(member.Name.Identifier.ValueText);
                }
            }
        }

        return [.. names];
    }

    private static NormalizedPayload BuildPayload(DataAccessMatch match)
    {
        var entries = new List<PayloadEntry>
        {
            new("operation", StructuralLiteral.Create(LiteralRole.ProtocolName, OperationLiteral(match.Operation), "operation")),
        };

        if (match.EntityTypeFqn is { } entityType)
        {
            entries.Add(new PayloadEntry("entity-type", StructuralLiteral.Create(LiteralRole.ProtocolName, entityType, "entity-type")));
        }

        if (match.ContextTypeFqn is { } contextType)
        {
            entries.Add(new PayloadEntry("context-type", StructuralLiteral.Create(LiteralRole.ProtocolName, contextType, "context-type")));
        }

        if (!match.FieldNames.IsDefaultOrEmpty)
        {
            entries.Add(
                new PayloadEntry(
                    "field-names",
                    StructuralLiteral.Create(LiteralRole.FieldName, string.Join('|', match.FieldNames), "field-names")));
        }

        if (match.SqlFacts is { } sql)
        {
            entries.Add(
                new PayloadEntry(
                    "sql-operation",
                    StructuralLiteral.Create(LiteralRole.ProtocolName, OperationLiteral(sql.Operation), "sql-operation")));
            entries.Add(new PayloadEntry("sql-target", StructuralLiteral.Create(LiteralRole.TableName, sql.Target, "sql-target")));

            if (!sql.Columns.IsDefaultOrEmpty)
            {
                var sortedColumns = string.Join('|', sql.Columns.OrderBy(static column => column, StringComparer.Ordinal));
                entries.Add(new PayloadEntry("sql-columns", StructuralLiteral.Create(LiteralRole.FieldName, sortedColumns, "sql-columns")));
            }
        }

        return NormalizedPayload.Create(entries);
    }

    private static string OperationLiteral(DataOperationKind operation) => operation switch
    {
        DataOperationKind.Read => "read",
        DataOperationKind.Insert => "insert",
        DataOperationKind.Update => "update",
        DataOperationKind.Delete => "delete",
        DataOperationKind.Execute => "execute",
        _ => "unknown",
    };

    private static string? Qualify(ITypeSymbol? type) => type?.ToDisplayString(Qualified);

    private static bool IsLinqOperator(IMethodSymbol method)
    {
        var containing = method.ReducedFrom?.ContainingType ?? method.ContainingType;
        return containing.Name is "Enumerable" or "Queryable"
            && containing.ContainingNamespace is { Name: "Linq", ContainingNamespace.Name: "System" };
    }

    private static bool TryDbSetEntityType(ITypeSymbol? type, Compilation compilation, out ITypeSymbol? entityType)
    {
        var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
        if (type is INamedTypeSymbol named
            && dbSet is not null
            && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, dbSet)
            && named.TypeArguments is [var argument])
        {
            entityType = argument;
            return true;
        }

        entityType = null;
        return false;
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
