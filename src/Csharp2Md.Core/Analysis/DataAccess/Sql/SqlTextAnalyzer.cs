using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.DataAccess.Sql;

/// <summary>
/// Finds SQL-shaped string expressions, routes readable literals through
/// <see cref="SqlStatementReader"/>, and marks interpolated or concatenated expressions unresolved
/// without inventing a target.
/// </summary>
/// <remarks>
/// A string is treated as SQL only when its first significant token is a recognised verb, which is the
/// guard DAD-15 rests on at the point of capture: a connection string opens with <c>Server=</c> or
/// <c>Data Source=</c>, never with a verb, so it is never captured. Syntax cannot prove that a literal
/// reached a database API, so a readable access is claimed at
/// <see cref="FactResolution.Syntactic"/> - the spec's Edge Case for a verb used as a message.
/// </remarks>
internal sealed class SqlTextAnalyzer : IDataAccessAnalyzer
{
    /// <summary>The stable identity every SQL claim carries as its detector provenance.</summary>
    public static DataAccessAnalyzerId AnalyzerId { get; } =
        DataAccessAnalyzerId.Create("csharp2md.dataaccess.sql");

    /// <summary>DAD-27: what a dynamic statement carries instead of an invented destination.</summary>
    internal const string DynamicTargetText = "dynamic-table";

    /// <summary>DAD-27: the reason a statement the analyzer could not reduce to a literal is unresolved.</summary>
    internal const string DynamicSqlReason = "dynamic-sql";

    /// <summary>DAD-28: the reason a readable statement's target stayed unresolved.</summary>
    internal const string UnreadableTargetReason = "unreadable-sql-target";

    public DataAccessAnalyzerId Id => AnalyzerId;

    public void Analyze(DataAccessContext context, ImmutableArray<RawDatabaseClaim>.Builder claims)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(claims);

        foreach (var expression in context.Root.DescendantNodes().OfType<ExpressionSyntax>())
        {
            if (IsStringExpression(expression) && !IsNestedInStringExpression(expression))
            {
                AnalyzeStringExpression(context, expression, claims);
            }
        }
    }

    private static void AnalyzeStringExpression(
        DataAccessContext context,
        ExpressionSyntax expression,
        ImmutableArray<RawDatabaseClaim>.Builder claims)
    {
        if (expression is LiteralExpressionSyntax { Token.Value: string literal })
        {
            AnalyzeLiteral(context, expression, literal, claims);
            return;
        }

        // DAD-27: an interpolation or a concatenation cannot be reduced to one literal. Its leading
        // text still proves the verb, so the access is claimed - with no object name at all.
        if (LeadingLiteralText(expression) is not { } leading
            || !SqlStatementReader.TryRead(leading, out var statement))
        {
            return;
        }

        claims.Add(new RawDatabaseClaim
        {
            Kind = DatabaseClaimKind.Access,
            OwnerId = context.OwnerOf(expression),
            Evidence = EvidenceFor(context, expression),
            ShapeConfidence = FactResolution.Unresolved,
            AnalyzerId = AnalyzerId,
            ObjectText = DynamicTargetText,
            Operation = statement.Operation,
            UnresolvedReason = DynamicSqlReason,
            SqlText = PreservableText(expression.ToString()),
        });
    }

    /// <summary>
    /// DAD-21, DAD-22, DAD-28: one access claim for the statement, plus one column claim per column the
    /// reader proved. <c>ObjectKind</c> is set only when the object's name came from the literal, so
    /// the resolver mints a node from a proven name and never from a placeholder.
    /// </summary>
    private static void AnalyzeLiteral(
        DataAccessContext context,
        ExpressionSyntax expression,
        string literal,
        ImmutableArray<RawDatabaseClaim>.Builder claims)
    {
        if (!SqlStatementReader.TryRead(literal, out var statement))
        {
            return;
        }

        var resolved = statement.Target is not null;
        var evidence = EvidenceFor(context, expression);
        var owner = context.OwnerOf(expression);
        claims.Add(new RawDatabaseClaim
        {
            Kind = DatabaseClaimKind.Access,
            OwnerId = owner,
            Evidence = evidence,
            ShapeConfidence = resolved ? FactResolution.Syntactic : FactResolution.Unresolved,
            AnalyzerId = AnalyzerId,
            ObjectText = statement.Target,
            ObjectKind = resolved ? statement.ObjectKind : null,
            Operation = statement.Operation,
            UnresolvedReason = resolved ? null : UnreadableTargetReason,
            SqlText = resolved ? null : PreservableText(literal),
        });

        AddColumnClaims(statement.WrittenColumns, ColumnUsage.Write);
        AddColumnClaims(statement.FilterColumns, ColumnUsage.Filter);

        void AddColumnClaims(ImmutableArray<string> columns, ColumnUsage usage)
        {
            foreach (var column in columns)
            {
                claims.Add(new RawDatabaseClaim
                {
                    Kind = DatabaseClaimKind.ColumnAccess,
                    OwnerId = owner,
                    Evidence = evidence,
                    ShapeConfidence = resolved ? FactResolution.Syntactic : FactResolution.Unresolved,
                    AnalyzerId = AnalyzerId,
                    ObjectText = statement.Target,
                    ColumnText = column,
                    Operation = statement.Operation,
                    Usage = usage,
                });
            }
        }
    }

    /// <summary>
    /// DAD-15: the statement text, or <c>null</c> when it assigns a literal credential value. The
    /// access is still claimed either way; only the text is withheld.
    /// </summary>
    private static string? PreservableText(string text) => CredentialText.Carries(text) ? null : text;

    /// <summary>The literal text that opens a string expression, or <c>null</c> when it does not open with one.</summary>
    private static string? LeadingLiteralText(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax { Token.Value: string literal } => literal,
        InterpolatedStringExpressionSyntax { Contents: [InterpolatedStringTextSyntax text, ..] } =>
            text.TextToken.ValueText,
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) =>
            LeadingLiteralText(binary.Left),
        _ => null,
    };

    private static bool IsStringExpression(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax { Token.Value: string } => true,
        InterpolatedStringExpressionSyntax => true,
        BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression) =>
            IsStringExpression(binary.Left) || IsStringExpression(binary.Right),
        _ => false,
    };

    /// <summary>
    /// Whether this expression is a fragment of a larger string expression. Only the outermost one is
    /// analysed, so a concatenation is read once rather than once per fragment.
    /// </summary>
    private static bool IsNestedInStringExpression(ExpressionSyntax expression) =>
        expression.Ancestors().Any(static ancestor =>
            ancestor is InterpolatedStringExpressionSyntax
                || (ancestor is BinaryExpressionSyntax binary && IsStringExpression(binary)));

    private static Evidence EvidenceFor(DataAccessContext context, SyntaxNode node)
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
}
