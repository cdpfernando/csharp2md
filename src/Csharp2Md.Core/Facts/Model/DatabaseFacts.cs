using Csharp2Md.Core.Facts.Identity;

namespace Csharp2Md.Core.Facts.Model;

/// <summary>
/// What a discovered database object is. <see cref="Unknown"/> is the honest default: a statement whose
/// verb proves an access but not the target's nature never claims to have found a table.
/// </summary>
public enum DatabaseObjectKind
{
    Table,
    View,
    Procedure,
    Function,
    Unknown,
}

/// <summary>
/// The precise operation an access performs, carried as the <c>operation</c> relation detail while the
/// relation kind carries only the coarse direction.
/// </summary>
public enum DatabaseOperation
{
    Read,
    Insert,
    Update,
    Delete,
    Execute,
    Unknown,
}

/// <summary>
/// How a column is used at an access site, carried as the <c>usage</c> relation detail.
/// </summary>
public enum ColumnUsage
{
    Read,
    Write,
    Filter,
    Join,
    Order,
    Group,
    Aggregate,
    Unknown,
}

/// <summary>
/// A database object whose name was proven by a source literal or explicit configuration. A name that is
/// only inferred never becomes a node, so this fact never carries a guessed <see cref="Name"/>.
/// </summary>
public sealed record DatabaseObjectFact(
    FactHeader Header,
    DatabaseObjectFactId ObjectId,
    string ConnectionName,
    DatabaseObjectKind Kind,
    string Name) : IFact;

/// <summary>
/// A column of a <see cref="DatabaseObjectFact"/>, on the same proven-name terms.
/// </summary>
public sealed record DatabaseColumnFact(
    FactHeader Header,
    DatabaseColumnFactId ColumnId,
    DatabaseObjectFactId ObjectId,
    string Name) : IFact;

/// <summary>
/// Wire names for the persistence enums. Each mapping is written out rather than derived by lowercasing,
/// so a member added later cannot acquire a wire name by accident.
/// </summary>
public static class DatabaseFactWire
{
    public static string Name(DatabaseObjectKind kind) => kind switch
    {
        DatabaseObjectKind.Table => "table",
        DatabaseObjectKind.View => "view",
        DatabaseObjectKind.Procedure => "procedure",
        DatabaseObjectKind.Function => "function",
        DatabaseObjectKind.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported database object kind."),
    };

    public static string Name(DatabaseOperation operation) => operation switch
    {
        DatabaseOperation.Read => "read",
        DatabaseOperation.Insert => "insert",
        DatabaseOperation.Update => "update",
        DatabaseOperation.Delete => "delete",
        DatabaseOperation.Execute => "execute",
        DatabaseOperation.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported database operation."),
    };

    public static string Name(ColumnUsage usage) => usage switch
    {
        ColumnUsage.Read => "read",
        ColumnUsage.Write => "write",
        ColumnUsage.Filter => "filter",
        ColumnUsage.Join => "join",
        ColumnUsage.Order => "order",
        ColumnUsage.Group => "group",
        ColumnUsage.Aggregate => "aggregate",
        ColumnUsage.Unknown => "unknown",
        _ => throw new ArgumentOutOfRangeException(nameof(usage), usage, "Unsupported column usage."),
    };
}
