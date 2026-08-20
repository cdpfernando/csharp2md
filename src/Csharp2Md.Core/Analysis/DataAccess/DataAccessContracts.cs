using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Csharp2Md.Core.Analysis.DataAccess;

/// <summary>
/// What a pass-one observation claims. A claim is never a fact: at this point most targets cannot be
/// resolved yet, because the configuration naming them commonly lives in a different document.
/// </summary>
internal enum DatabaseClaimKind
{
    ContextDeclared,
    EntitySetExposed,
    TableConfigured,
    ColumnConfigured,
    Access,
    ColumnAccess,
}

/// <summary>
/// The stable reverse-DNS identity of a data access analyzer, which becomes the emitted fact's
/// <see cref="DetectorId"/>.
/// </summary>
internal readonly record struct DataAccessAnalyzerId
{
    private readonly DetectorId _id;

    private DataAccessAnalyzerId(DetectorId id) => _id = id;

    public string Value => _id.Value;

    public static DataAccessAnalyzerId Create(string name) => new(DetectorId.Create(name));

    public DetectorId ToDetectorId() => _id;

    public override string ToString() => Value;
}

/// <summary>
/// One pass-one observation. Never serialized: the mapping resolver turns claims into
/// facts once the whole run has been seen.
/// </summary>
internal sealed record RawDatabaseClaim
{
    /// <summary>DAD-16: the cap on preserved SQL text.</summary>
    internal const int SqlTextLimit = 2000;

    private readonly Evidence _evidence;
    private readonly string? _sqlText;

    public required DatabaseClaimKind Kind { get; init; }

    /// <summary>The enclosing member's symbol id, or the document's id when there is no member.</summary>
    public required FactId OwnerId { get; init; }

    /// <summary>DAD-13: enforced here, so a claim without evidence cannot exist.</summary>
    public required Evidence Evidence
    {
        get => _evidence;
        init => _evidence = RequireEvidence(value);
    }

    public required FactResolution ShapeConfidence { get; init; }

    public required DataAccessAnalyzerId AnalyzerId { get; init; }

    public string? EntityText { get; init; }

    public string? PropertyText { get; init; }

    public string? ObjectText { get; init; }

    public DatabaseObjectKind? ObjectKind { get; init; }

    public string? ColumnText { get; init; }

    public DatabaseOperation Operation { get; init; } = DatabaseOperation.Unknown;

    public ColumnUsage Usage { get; init; } = ColumnUsage.Unknown;

    /// <summary>DAD-16: capped at construction, so no later stage can widen it.</summary>
    public string? SqlText
    {
        get => _sqlText;
        init => _sqlText = Truncate(value);
    }

    public string? UnresolvedReason { get; init; }

    private static Evidence RequireEvidence(Evidence evidence) =>
        evidence == default
            ? throw new ArgumentException("A database claim requires evidence.", nameof(evidence))
            : evidence;

    private static string? Truncate(string? sqlText) =>
        sqlText is { Length: > SqlTextLimit } ? sqlText[..SqlTextLimit] : sqlText;
}

/// <summary>
/// Everything a data access analyzer may see: one parsed document plus the owner map the
/// syntax walk already built. No engine, no store, no I/O.
/// </summary>
internal sealed class DataAccessContext
{
    private readonly IReadOnlyDictionary<MemberDeclarationSyntax, SymbolFactId> _ownerByDeclaration;

    private DataAccessContext(
        DocumentFactId documentId,
        string relativePath,
        SyntaxNode root,
        IReadOnlyDictionary<MemberDeclarationSyntax, SymbolFactId> ownerByDeclaration)
    {
        DocumentId = documentId;
        RelativePath = relativePath;
        Root = root;
        _ownerByDeclaration = ownerByDeclaration;
    }

    public DocumentFactId DocumentId { get; }

    public string RelativePath { get; }

    public SyntaxNode Root { get; }

    public static DataAccessContext Create(
        DocumentFactId documentId,
        string relativePath,
        SyntaxNode root,
        IReadOnlyDictionary<MemberDeclarationSyntax, SymbolFactId> ownerByDeclaration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(ownerByDeclaration);
        return new DataAccessContext(documentId, relativePath, root, ownerByDeclaration);
    }

    /// <summary>
    /// The nearest enclosing declared member's symbol id, falling back to the document's own id only
    /// for a node with no declared enclosing member.
    /// </summary>
    public FactId OwnerOf(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        foreach (var declaration in node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>())
        {
            if (_ownerByDeclaration.TryGetValue(declaration, out var symbolId))
            {
                return symbolId.ToFactId();
            }
        }

        return DocumentId.ToFactId();
    }
}
