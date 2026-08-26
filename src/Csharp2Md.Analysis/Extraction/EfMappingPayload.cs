using Csharp2Md.Domain.Literals;
using Csharp2Md.Domain.Observations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Analysis.Extraction;

/// <summary>
/// Builds the payload entries for an EF Core fluent-mapping invocation - <c>Entity&lt;T&gt;()</c>,
/// <c>ToTable(string)</c>, <c>Property(...)</c> and <c>HasColumnName(string)</c> - so
/// <c>PersistenceModelBuilder</c> can promote explicit table and column mappings without re-reading
/// Roslyn (AD-004). A non-constant argument yields no literal entry, which is what lets the
/// conventional-mapping fallback (PK-16, PK-26) take over.
/// </summary>
internal static class EfMappingPayload
{
    private static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;

    public static IReadOnlyList<PayloadEntry> For(
        SemanticModel model,
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(invocation);

        var compilation = model.Compilation;
        var entries = new List<PayloadEntry>();

        if (IsModelBuilderEntity(method, compilation))
        {
            if (method.TypeArguments is [var ownEntityType])
            {
                entries.Add(EntityTypeEntry(ownEntityType));
            }

            return entries;
        }

        if (IsEntityTypeBuilderToTable(method, compilation))
        {
            AddLiteralIfConstant(entries, invocation, "table-name", LiteralRole.TableName);
            AddEnclosingEntityType(model, compilation, invocation, cancellationToken, entries);
            return entries;
        }

        if (IsEntityTypeBuilderProperty(method, compilation))
        {
            AddEnclosingEntityType(model, compilation, invocation, cancellationToken, entries);
            return entries;
        }

        if (IsPropertyBuilderHasColumnName(method, compilation))
        {
            AddLiteralIfConstant(entries, invocation, "field-name", LiteralRole.FieldName);
            AddEnclosingEntityType(model, compilation, invocation, cancellationToken, entries);
            if (TryFindPropertyName(model, compilation, invocation, cancellationToken, out var propertyName))
            {
                entries.Add(new PayloadEntry("property-name", StructuralLiteral.Create(LiteralRole.FieldName, propertyName, "property-name")));
            }

            return entries;
        }

        return entries;
    }

    private static void AddLiteralIfConstant(
        List<PayloadEntry> entries,
        InvocationExpressionSyntax invocation,
        string key,
        LiteralRole role)
    {
        var value = TryFirstStringLiteral(invocation);
        if (value is not null)
        {
            entries.Add(new PayloadEntry(key, StructuralLiteral.Create(role, value, key)));
        }
    }

    private static void AddEnclosingEntityType(
        SemanticModel model,
        Compilation compilation,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken,
        List<PayloadEntry> entries)
    {
        if (TryFindEnclosingEntityType(model, compilation, invocation, cancellationToken, out var entityType))
        {
            entries.Add(EntityTypeEntry(entityType!));
        }
    }

    private static PayloadEntry EntityTypeEntry(ITypeSymbol entityType) =>
        new("entity-type", StructuralLiteral.Create(LiteralRole.ProtocolName, entityType.ToDisplayString(Qualified), "entity-type"));

    /// <summary>
    /// Walks the receiver chain outward from <paramref name="invocation"/> until it finds the
    /// enclosing <c>ModelBuilder.Entity&lt;TEntity&gt;()</c> call, recovering <c>TEntity</c> - the
    /// table and column configuration lives in a document apart from the entity it configures, so
    /// this is how the cross-document mapping in <c>OrderConfiguration.cs</c> resolves.
    /// </summary>
    private static bool TryFindEnclosingEntityType(
        SemanticModel model,
        Compilation compilation,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken,
        out ITypeSymbol? entityType)
    {
        ExpressionSyntax? current = invocation.Expression is MemberAccessExpressionSyntax member ? member.Expression : null;
        while (current is not null)
        {
            if (current is InvocationExpressionSyntax candidate
                && model.GetSymbolInfo(candidate, cancellationToken).Symbol is IMethodSymbol candidateMethod
                && IsModelBuilderEntity(candidateMethod, compilation)
                && candidateMethod.TypeArguments is [var argument])
            {
                entityType = argument;
                return true;
            }

            current = current switch
            {
                InvocationExpressionSyntax inv when inv.Expression is MemberAccessExpressionSyntax m => m.Expression,
                MemberAccessExpressionSyntax m => m.Expression,
                _ => null,
            };
        }

        entityType = null;
        return false;
    }

    /// <summary>Recovers the CLR property name from the immediate <c>Property(x => x.Member)</c> receiver.</summary>
    private static bool TryFindPropertyName(
        SemanticModel model,
        Compilation compilation,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken,
        out string propertyName)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax member
            && member.Expression is InvocationExpressionSyntax propertyInvocation
            && model.GetSymbolInfo(propertyInvocation, cancellationToken).Symbol is IMethodSymbol propertyMethod
            && IsEntityTypeBuilderProperty(propertyMethod, compilation)
            && propertyInvocation.ArgumentList.Arguments is [{ Expression: SimpleLambdaExpressionSyntax lambda }]
            && lambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            propertyName = memberAccess.Name.Identifier.ValueText;
            return true;
        }

        propertyName = string.Empty;
        return false;
    }

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

    private static bool IsModelBuilderEntity(IMethodSymbol method, Compilation compilation) =>
        method.Name is "Entity" && IsType(method.ContainingType, compilation, "Microsoft.EntityFrameworkCore.ModelBuilder");

    private static bool IsEntityTypeBuilderToTable(IMethodSymbol method, Compilation compilation) =>
        method.Name is "ToTable" && IsType(method.ContainingType, compilation, "Microsoft.EntityFrameworkCore.EntityTypeBuilder`1");

    private static bool IsEntityTypeBuilderProperty(IMethodSymbol method, Compilation compilation) =>
        method.Name is "Property" && IsType(method.ContainingType, compilation, "Microsoft.EntityFrameworkCore.EntityTypeBuilder`1");

    private static bool IsPropertyBuilderHasColumnName(IMethodSymbol method, Compilation compilation) =>
        method.Name is "HasColumnName" && IsType(method.ContainingType, compilation, "Microsoft.EntityFrameworkCore.PropertyBuilder");

    private static bool IsType(INamedTypeSymbol? type, Compilation compilation, string metadataName)
    {
        var expected = compilation.GetTypeByMetadataName(metadataName);
        return type is not null && expected is not null && SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, expected);
    }
}
