using System.Collections.Frozen;
using Microsoft.CodeAnalysis;

namespace Csharp2Md.Core.Analysis.Relations;

/// <summary>
/// One shared BCL/framework exclusion rule, used identically by <c>calls</c> and <c>creates</c>
/// candidate detection so their volume stays bounded without diverging denylists.
/// </summary>
internal static class RelationNoiseFilter
{
    private static readonly FrozenSet<string> Denylist = new[]
    {
        "List",
        "Dictionary",
        "HashSet",
        "Queue",
        "Stack",
        "StringBuilder",
        "Guid",
        "Uri",
        "TimeSpan",
        "DateTime",
        "DateTimeOffset",
        "Task",
        "CancellationTokenSource",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// Syntax-only fallback: a small, explicit denylist of common BCL/collection simple names,
    /// plus any name ending in "Exception".
    /// </summary>
    public static bool IsLikelyFrameworkType(string simpleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(simpleName);

        return Denylist.Contains(simpleName) ||
            simpleName.EndsWith("Exception", StringComparison.Ordinal);
    }

    /// <summary>
    /// Semantic path: the resolved type's containing namespace starts with "System" or
    /// "Microsoft.Extensions".
    /// </summary>
    public static bool IsFrameworkType(ITypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var containingNamespace = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return IsOrStartsWith(containingNamespace, "System") ||
            IsOrStartsWith(containingNamespace, "Microsoft.Extensions");
    }

    private static bool IsOrStartsWith(string value, string prefix) =>
        value.Equals(prefix, StringComparison.Ordinal) ||
        value.StartsWith(prefix + ".", StringComparison.Ordinal);
}
