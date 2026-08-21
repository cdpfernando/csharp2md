using System.Collections.Frozen;
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

/// <summary>One entity property a LINQ chain references, and how it was used.</summary>
internal readonly record struct ColumnReference(
    string PropertyName,
    ColumnUsage Usage,
    MemberAccessExpressionSyntax Reference);

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

        var entitySets = DiscoverEntitySets(context);
        foreach (var entitySet in entitySets)
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

        foreach (var invocation in context.Root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            AnalyzeInvocation(context, invocation, claims);
        }

        AnalyzeEntitySetAccesses(context, EntityByEntitySetName(entitySets), claims);
        AnalyzeTrackedWrites(context, claims);
    }

    /// <summary>
    /// DAD-11, claim side: a property assignment inside a member that also calls
    /// <c>SaveChanges</c>/<c>SaveChangesAsync</c> is a tracked write. The claim records only what was
    /// observed - the property's own name and the receiver text - because syntax cannot prove which
    /// entity the receiver holds. Attribution belongs to the mapping resolver.
    /// </summary>
    private static void AnalyzeTrackedWrites(
        DataAccessContext context,
        ImmutableArray<RawDatabaseClaim>.Builder claims)
    {
        foreach (var member in context.Root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            if (!SavesChanges(member))
            {
                continue;
            }

            foreach (var assignment in member.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is not MemberAccessExpressionSyntax target)
                {
                    continue;
                }

                claims.Add(new RawDatabaseClaim
                {
                    Kind = DatabaseClaimKind.ColumnAccess,
                    OwnerId = context.OwnerOf(assignment),
                    Evidence = EvidenceFor(context, target),
                    ShapeConfidence = FactResolution.Syntactic,
                    AnalyzerId = AnalyzerId,
                    PropertyText = target.Name.Identifier.ValueText,
                    ColumnText = NormalizeNode(target),
                    Usage = ColumnUsage.Write,
                });
            }
        }
    }

    private static bool SavesChanges(BaseMethodDeclarationSyntax member) =>
        member.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(static invocation =>
                InvokedMemberName(invocation) is "SaveChanges" or "SaveChangesAsync");

    private static string NormalizeNode(SyntaxNode node) =>
        node.WithoutTrivia().NormalizeWhitespace(indentation: " ", eol: " ", elasticTrivia: false).ToFullString();

    /// <summary>
    /// Every read of a discovered <c>DbSet</c> property, plus the entity properties the LINQ chain built
    /// on it references. Only a qualified <c>receiver.Set</c> access counts: a bare identifier cannot be
    /// told from an unrelated local of the same name without semantics.
    /// </summary>
    private static void AnalyzeEntitySetAccesses(
        DataAccessContext context,
        IReadOnlyDictionary<string, string> entityByEntitySetName,
        ImmutableArray<RawDatabaseClaim>.Builder claims)
    {
        if (entityByEntitySetName.Count == 0)
        {
            return;
        }

        foreach (var access in context.Root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var entitySetName = access.Name.Identifier.ValueText;
            if (!entityByEntitySetName.TryGetValue(entitySetName, out var entityName))
            {
                continue;
            }

            var chain = ChainFrom(access);
            var write = WriteOperationOn(access);
            claims.Add(new RawDatabaseClaim
            {
                Kind = DatabaseClaimKind.Access,
                OwnerId = context.OwnerOf(access),
                Evidence = EvidenceFor(context, write?.Invocation ?? (SyntaxNode)access),
                ShapeConfidence = FactResolution.Syntactic,
                AnalyzerId = AnalyzerId,
                EntityText = entityName,
                PropertyText = entitySetName,
                Operation = write?.Operation ?? DatabaseOperation.Read,
            });

            foreach (var column in ChainColumns(chain))
            {
                claims.Add(new RawDatabaseClaim
                {
                    Kind = DatabaseClaimKind.ColumnAccess,
                    OwnerId = context.OwnerOf(column.Reference),
                    Evidence = EvidenceFor(context, column.Reference),
                    ShapeConfidence = FactResolution.Syntactic,
                    AnalyzerId = AnalyzerId,
                    EntityText = entityName,
                    PropertyText = column.PropertyName,
                    Usage = column.Usage,
                });
            }
        }
    }

    /// <summary>
    /// One entity per <c>DbSet</c> property name. The first declaration wins, so a document declaring two
    /// contexts with a same-named set resolves in declaration order rather than arbitrarily.
    /// </summary>
    private static Dictionary<string, string> EntityByEntitySetName(
        ImmutableArray<DiscoveredEntitySet> entitySets)
    {
        var byName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entitySet in entitySets)
        {
            byName.TryAdd(entitySet.PropertyName, entitySet.EntityName);
        }

        return byName;
    }

    /// <summary>
    /// DAD-10: the <c>DbSet</c> methods that prove a write, and the operation each one performs.
    /// </summary>
    private static readonly FrozenDictionary<string, DatabaseOperation> WriteOperations =
        new Dictionary<string, DatabaseOperation>(StringComparer.Ordinal)
        {
            ["Add"] = DatabaseOperation.Insert,
            ["AddAsync"] = DatabaseOperation.Insert,
            ["AddRange"] = DatabaseOperation.Insert,
            ["Update"] = DatabaseOperation.Update,
            ["UpdateRange"] = DatabaseOperation.Update,
            ["Remove"] = DatabaseOperation.Delete,
            ["RemoveRange"] = DatabaseOperation.Delete,
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// The write this entity set access performs, when the access is itself the receiver of one of the
    /// write-family methods. The same method name on any other receiver proves nothing.
    /// </summary>
    private static (DatabaseOperation Operation, InvocationExpressionSyntax Invocation)? WriteOperationOn(
        MemberAccessExpressionSyntax access) =>
        access.Parent is MemberAccessExpressionSyntax parent
            && ReferenceEquals(parent.Expression, access)
            && parent.Parent is InvocationExpressionSyntax invocation
            && WriteOperations.TryGetValue(parent.Name.Identifier.ValueText, out var operation)
                ? (operation, invocation)
                : null;

    /// <summary>The invocations chained directly onto an expression, outermost last.</summary>
    private static ImmutableArray<InvocationExpressionSyntax> ChainFrom(ExpressionSyntax expression)
    {
        var chain = ImmutableArray.CreateBuilder<InvocationExpressionSyntax>();
        var current = (ExpressionSyntax)expression;
        while (current.Parent is MemberAccessExpressionSyntax parent
            && ReferenceEquals(parent.Expression, current)
            && parent.Parent is InvocationExpressionSyntax invocation)
        {
            chain.Add(invocation);
            current = invocation;
        }

        return chain.ToImmutable();
    }

    /// <summary>
    /// DAD-08 and DAD-09: every entity property a chain operator's lambda references, in chain order,
    /// de-duplicated per property and usage so a property named twice in one projection is one column
    /// claim while a property both filtered on and projected stays two.
    /// </summary>
    private static ImmutableArray<ColumnReference> ChainColumns(
        ImmutableArray<InvocationExpressionSyntax> chain)
    {
        var columns = ImmutableArray.CreateBuilder<ColumnReference>();
        var seen = new HashSet<(string PropertyName, ColumnUsage Usage)>();
        foreach (var invocation in chain)
        {
            foreach (var argument in invocation.ArgumentList.Arguments)
            {
                if (argument.Expression is not LambdaExpressionSyntax lambda)
                {
                    continue;
                }

                var usage = InvokedMemberName(invocation) == "Where"
                    ? ColumnUsage.Filter
                    : ColumnUsage.Read;
                foreach (var reference in LambdaPropertyReferences(lambda))
                {
                    var column = new ColumnReference(
                        reference.Name.Identifier.ValueText, usage, reference);
                    if (seen.Add((column.PropertyName, column.Usage)))
                    {
                        columns.Add(column);
                    }
                }
            }
        }

        return columns.ToImmutable();
    }

    /// <summary>Every <c>parameter.Property</c> access inside a lambda's body.</summary>
    private static IEnumerable<MemberAccessExpressionSyntax> LambdaPropertyReferences(
        LambdaExpressionSyntax lambda)
    {
        var parameters = LambdaParameterNames(lambda);
        if (parameters.Count == 0 || lambda.Body is null)
        {
            return [];
        }

        return lambda.Body.DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Where(access => access.Expression is IdentifierNameSyntax identifier
                && parameters.Contains(identifier.Identifier.ValueText));
    }

    private static IReadOnlyCollection<string> LambdaParameterNames(LambdaExpressionSyntax lambda) =>
        lambda switch
        {
            SimpleLambdaExpressionSyntax simple => [simple.Parameter.Identifier.ValueText],
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters
                .Select(static parameter => parameter.Identifier.ValueText)
                .ToArray(),
            _ => [],
        };

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
