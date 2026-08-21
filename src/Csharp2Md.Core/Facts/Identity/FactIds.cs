using System.Text.RegularExpressions;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Facts.Identity;

public readonly record struct ProjectFactId
{
    private readonly FactId _id;

    public string Value => _id.Value;

    public static ProjectFactId Create(string relativeProjectPath) =>
        new(FactIdGrammar.Create("project", ("path", FactIdGrammar.ValidateRelativePath(relativeProjectPath, nameof(relativeProjectPath)))));

    private ProjectFactId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct TargetFactId
{
    private readonly FactId _id;

    public string Value => _id.Value;

    public static TargetFactId Create(ProjectFactId projectId, string targetFramework) =>
        new(FactIdGrammar.Create(
            "target",
            ("project", projectId.Value),
            ("tfm", FactIdGrammar.RequireCanonicalText(targetFramework, nameof(targetFramework)))));

    private TargetFactId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct DocumentFactId
{
    private readonly FactId _id;

    public string Value => _id.Value;

    public static DocumentFactId Create(ProjectFactId projectId, string relativeDocumentPath) =>
        new(FactIdGrammar.Create(
            "document",
            ("project", projectId.Value),
            ("path", FactIdGrammar.ValidateRelativePath(relativeDocumentPath, nameof(relativeDocumentPath)))));

    private DocumentFactId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct SymbolFactId
{
    private readonly FactId _id;

    public string Value => _id.Value;

    public static SymbolFactId CreateResolved(TargetFactId targetId, string documentationCommentId) =>
        new(FactIdGrammar.Create(
            "symbol",
            ("target", targetId.Value),
            ("doc", FactIdGrammar.RequireCanonicalText(documentationCommentId, nameof(documentationCommentId)))));

    public static SymbolFactId CreateFallback(TargetFactId targetId, CanonicalSymbolSignature signature) =>
        new(FactIdGrammar.Create("symbol", ("target", targetId.Value), ("doc", signature.Value)));

    public static SymbolFactId CreateSyntactic(
        ProjectFactId projectId,
        string relativeDocumentPath,
        string declarationKind,
        string normalizedDeclarationSignature) =>
        new(FactIdGrammar.Create(
            "syntactic-symbol",
            ("project", projectId.Value),
            ("document", FactIdGrammar.ValidateRelativePath(relativeDocumentPath, nameof(relativeDocumentPath))),
            ("kind", FactIdGrammar.RequireCanonicalText(declarationKind, nameof(declarationKind))),
            ("signature", FactIdGrammar.RequireCanonicalText(normalizedDeclarationSignature, nameof(normalizedDeclarationSignature)))));

    private SymbolFactId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct ComponentFactId
{
    private readonly FactId _id;

    public string Value => _id.Value;

    public static ComponentFactId Create(string componentKind, IEnumerable<FactId> ownerIds)
    {
        ArgumentNullException.ThrowIfNull(ownerIds);
        var owners = ownerIds
            .Select(static id => id.Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (owners.Length == 0)
        {
            throw new ArgumentException("At least one owner identity is required.", nameof(ownerIds));
        }

        return new ComponentFactId(FactIdGrammar.Create(
            "component",
            ("kind", FactIdGrammar.RequireCanonicalText(componentKind, nameof(componentKind))),
            ("owners", string.Join(',', owners))));
    }

    private ComponentFactId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct RelationFactId
{
    private readonly FactId _id;

    public string Value => _id.Value;

    public static RelationFactId Create(FactId ownerId, string relationKind, string claimFingerprint, int ordinal)
    {
        if (ordinal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), "The occurrence ordinal must be one-based.");
        }

        return new RelationFactId(FactIdGrammar.Create(
            "relation",
            ("owner", ownerId.Value),
            ("kind", FactIdGrammar.RequireCanonicalText(relationKind, nameof(relationKind))),
            ("claim", FactIdGrammar.RequireCanonicalText(claimFingerprint, nameof(claimFingerprint))),
            ("ordinal", ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture))));
    }

    private RelationFactId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct DatabaseObjectFactId
{
    /// <summary>
    /// The connection component is present from the start so that resolving a real connection later fills
    /// a component the grammar already has, instead of invalidating every previously minted node id.
    /// </summary>
    public const string UnknownConnection = "unknown";

    private readonly FactId _id;

    public string Value => _id.Value;

    public static DatabaseObjectFactId Create(string connection, DatabaseObjectKind kind, string name) =>
        new(FactIdGrammar.Create(
            "database-object",
            ("connection", FactIdGrammar.RequireCanonicalText(connection, nameof(connection))),
            ("kind", DatabaseFactWire.Name(kind)),
            ("name", FactIdGrammar.RequireCanonicalText(name, nameof(name)))));

    private DatabaseObjectFactId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct DatabaseColumnFactId
{
    private readonly FactId _id;

    public string Value => _id.Value;

    public static DatabaseColumnFactId Create(DatabaseObjectFactId objectId, string name) =>
        new(FactIdGrammar.Create(
            "database-column",
            ("object", objectId.Value),
            ("name", FactIdGrammar.RequireCanonicalText(name, nameof(name)))));

    private DatabaseColumnFactId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct DiagnosticId
{
    private readonly FactId _id;

    public string Value => _id.Value;

    public static DiagnosticId Create(string stage, FactId scopeId, string code, string fingerprint) =>
        new(FactIdGrammar.Create(
            "diagnostic",
            ("stage", FactIdGrammar.RequireCanonicalText(stage, nameof(stage))),
            ("scope", scopeId.Value),
            ("code", FactIdGrammar.RequireCanonicalText(code, nameof(code))),
            ("fingerprint", FactIdGrammar.RequireCanonicalText(fingerprint, nameof(fingerprint)))));

    private DiagnosticId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}

public readonly record struct DetectorId
{
    private static readonly Regex NamePattern = new(
        "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?(?:\\.[a-z0-9](?:[a-z0-9-]*[a-z0-9])?)+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    private readonly FactId _id;

    public string Value => _id.Value;

    public static DetectorId Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!NamePattern.IsMatch(name))
        {
            throw new ArgumentException("Detector names must use lower-ASCII reverse-DNS form.", nameof(name));
        }

        return new DetectorId(FactIdGrammar.Create("detector", ("name", name)));
    }

    private DetectorId(FactId id) => _id = id;

    public FactId ToFactId() => _id;

    public override string ToString() => Value;
}
