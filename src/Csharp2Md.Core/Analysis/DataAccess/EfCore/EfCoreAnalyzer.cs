using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.DataAccess.EfCore;

/// <summary>
/// One <c>DbSet&lt;TEntity&gt;</c> property a <c>DbContext</c> subclass declares in the analysed document.
/// </summary>
internal readonly record struct DiscoveredEntitySet(
    TypeDeclarationSyntax Context,
    PropertyDeclarationSyntax Declaration,
    string PropertyName,
    string EntityName);

/// <summary>
/// Recognises the EF Core shapes P1 covers and appends one raw claim per observation. Nothing here
/// resolves a target: the configuration that names an entity's table commonly lives in another document,
/// so correlation is the mapping resolver's job.
/// </summary>
/// <remarks>
/// The seam hands an analyzer one parsed document at a time, so the entity-set catalogue this analyzer
/// builds is per document: a query whose <c>DbContext</c> is declared elsewhere is not recognised here.
/// Widening that is the cross-document resolver's concern, not the claim pass's.
/// </remarks>
internal sealed class EfCoreAnalyzer : IDataAccessAnalyzer
{
    /// <summary>The stable identity every EF Core claim carries as its detector provenance.</summary>
    public static DataAccessAnalyzerId AnalyzerId { get; } =
        DataAccessAnalyzerId.Create("csharp2md.dataaccess.efcore");

    public DataAccessAnalyzerId Id => AnalyzerId;

    public void Analyze(DataAccessContext context, ImmutableArray<RawDatabaseClaim>.Builder claims)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(claims);

        foreach (var invocation in context.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            AnalyzeInvocation(context, invocation, claims);
        }

        foreach (var entitySet in DiscoverEntitySets(context))
        {
            // DAD-01: sourced at the declaring context type, evidenced at the property that proves it.
            claims.Add(new RawDatabaseClaim
            {
                Kind = DatabaseClaimKind.EntitySetExposed,
                OwnerId = context.OwnerOf(entitySet.Context),
                Evidence = EvidenceFor(context, entitySet.Declaration),
                ShapeConfidence = FactResolution.Syntactic,
                AnalyzerId = AnalyzerId,
                EntityText = entitySet.EntityName,
                PropertyText = entitySet.PropertyName,
            });
        }
    }

    private static void AnalyzeInvocation(
        DataAccessContext context,
        InvocationExpressionSyntax invocation,
        ImmutableArray<RawDatabaseClaim>.Builder claims)
    {
        if (InvokedMemberName(invocation) is not { } memberName)
        {
            return;
        }

        if (memberName == "ToTable"
            && FirstLiteralArgument(invocation) is { } tableName
            && ConfiguredEntityName(invocation.Expression) is { } entityName)
        {
            // DAD-03: a literal proves the table's name, which is the only thing that mints a node.
            claims.Add(new RawDatabaseClaim
            {
                Kind = DatabaseClaimKind.TableConfigured,
                OwnerId = context.OwnerOf(invocation),
                Evidence = EvidenceFor(context, invocation),
                ShapeConfidence = FactResolution.Exact,
                AnalyzerId = AnalyzerId,
                EntityText = entityName,
                ObjectText = tableName,
                ObjectKind = DatabaseObjectKind.Table,
            });
        }

        if (memberName == "HasColumnName"
            && FirstLiteralArgument(invocation) is { } columnName
            && ConfiguredPropertyName(invocation.Expression) is { } propertyName
            && ConfiguredEntityName(invocation.Expression) is { } columnEntityName)
        {
            // DAD-05: the column's name is proven; the property it belongs to is read off the
            // Property(x => x.P) selector rather than guessed from the column text.
            claims.Add(new RawDatabaseClaim
            {
                Kind = DatabaseClaimKind.ColumnConfigured,
                OwnerId = context.OwnerOf(invocation),
                Evidence = EvidenceFor(context, invocation),
                ShapeConfidence = FactResolution.Exact,
                AnalyzerId = AnalyzerId,
                EntityText = columnEntityName,
                PropertyText = propertyName,
                ColumnText = columnName,
            });
        }
    }

    /// <summary>
    /// The property named by the nearest <c>Property(x =&gt; x.P)</c> selector in this expression's own
    /// receiver chain, or <c>null</c> when there is none to read.
    /// </summary>
    private static string? ConfiguredPropertyName(ExpressionSyntax expression)
    {
        var cursor = expression;
        while (true)
        {
            switch (cursor)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    cursor = memberAccess.Expression;
                    break;
                case InvocationExpressionSyntax nested:
                    if (InvokedMemberName(nested) == "Property"
                        && nested.ArgumentList.Arguments is [{ Expression: LambdaExpressionSyntax lambda }]
                        && lambda.Body is MemberAccessExpressionSyntax selected)
                    {
                        return selected.Name.Identifier.ValueText;
                    }

                    cursor = nested.Expression;
                    break;
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// The entity named by the nearest <c>Entity&lt;TEntity&gt;()</c> invocation in this expression's own
    /// receiver chain. A configuration chain split across statements hides the entity from syntax, so it
    /// returns <c>null</c> and the caller claims nothing rather than mis-attributing the configuration.
    /// </summary>
    private static string? ConfiguredEntityName(ExpressionSyntax expression)
    {
        var cursor = expression;
        while (true)
        {
            switch (cursor)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    if (memberAccess.Name is GenericNameSyntax
                        {
                            Identifier.ValueText: "Entity",
                            TypeArgumentList.Arguments: [var entityType],
                        })
                    {
                        return SimpleTypeName(entityType);
                    }

                    cursor = memberAccess.Expression;
                    break;
                case InvocationExpressionSyntax nested:
                    cursor = nested.Expression;
                    break;
                default:
                    return null;
            }
        }
    }

    private static string? InvokedMemberName(InvocationExpressionSyntax invocation) =>
        invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? memberAccess.Name.Identifier.ValueText
            : null;

    private static string? FirstLiteralArgument(InvocationExpressionSyntax invocation) =>
        invocation.ArgumentList.Arguments is [{ Expression: LiteralExpressionSyntax { Token.Value: string literal } }, ..]
            ? literal
            : null;

    /// <summary>
    /// Every <c>DbSet&lt;TEntity&gt;</c> property declared by a type whose base list names
    /// <c>DbContext</c>, in declaration order. A <c>DbSet</c> on any other type is not an entity set
    /// (spec Edge Case), and an entity whose simple name is a framework shape is filtered out by the same
    /// rule <c>calls</c> and <c>creates</c> already use.
    /// </summary>
    internal static ImmutableArray<DiscoveredEntitySet> DiscoverEntitySets(DataAccessContext context)
    {
        var sets = ImmutableArray.CreateBuilder<DiscoveredEntitySet>();
        foreach (var declaration in context.Root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (!DerivesFromDbContext(declaration))
            {
                continue;
            }

            foreach (var property in declaration.Members.OfType<PropertyDeclarationSyntax>())
            {
                if (AsEntitySetType(property.Type) is not { TypeArgumentList.Arguments: [var entityType] })
                {
                    continue;
                }

                var entityName = SimpleTypeName(entityType);
                if (RelationNoiseFilter.IsLikelyFrameworkType(entityName))
                {
                    continue;
                }

                sets.Add(new DiscoveredEntitySet(
                    declaration, property, property.Identifier.ValueText, entityName));
            }
        }

        return sets.ToImmutable();
    }

    internal static Evidence EvidenceFor(DataAccessContext context, SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new Evidence(
            context.DocumentId,
            context.RelativePath,
            span.StartLinePosition.Line + 1,
            span.StartLinePosition.Character + 1,
            span.EndLinePosition.Line + 1,
            span.EndLinePosition.Character + 1);
    }

    private static bool DerivesFromDbContext(TypeDeclarationSyntax declaration) =>
        declaration.BaseList?.Types
            .Any(static baseType => SimpleTypeName(baseType.Type) == "DbContext") ?? false;

    private static GenericNameSyntax? AsEntitySetType(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => AsEntitySetType(qualified.Right),
        AliasQualifiedNameSyntax alias => AsEntitySetType(alias.Name),
        GenericNameSyntax { Identifier.ValueText: "DbSet", TypeArgumentList.Arguments.Count: 1 } generic => generic,
        _ => null,
    };

    private static string SimpleTypeName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => SimpleTypeName(qualified.Right),
        AliasQualifiedNameSyntax alias => SimpleTypeName(alias.Name),
        NullableTypeSyntax nullable => SimpleTypeName(nullable.ElementType),
        GenericNameSyntax generic => generic.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => type.ToString(),
    };
}
