using System.Collections.Frozen;
using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Model;

namespace Csharp2Md.Core.Analysis.Indexes;

/// <summary>
/// The queryable surface every consumer of the index sees. Kept separate from
/// <see cref="SymbolIndex"/> so a future consumer (a relation resolver) depends on the query
/// contract rather than on the concrete <see cref="FrozenDictionary"/>-backed implementation.
/// </summary>
internal interface ISymbolIndex
{
    SymbolFact? GetById(SymbolFactId id);

    ImmutableArray<SymbolFact> FindByName(string simpleName);

    ImmutableArray<SymbolFact> FindByQualifiedName(string fullyQualifiedName);

    ImmutableArray<SymbolFact> FindMembers(string containingType, string memberName);

    ImmutableArray<SymbolFact> FindMethods(MethodLookup lookup);
}

/// <summary>
/// The query shape for <see cref="ISymbolIndex.FindMethods"/>. <see cref="ArgumentCount"/> filters
/// candidates to that exact declared parameter count; <see cref="ArgumentTypes"/> only ranks them,
/// never discards. <see cref="Namespace"/>, <see cref="ProjectId"/> and <see cref="Imports"/> are
/// carried for the caller's benefit but do not narrow a method lookup - spec.md assigns contextual
/// hints to <c>FindCandidates</c>, and defines no <c>FindMethods</c> behaviour for them.
/// </summary>
internal sealed record MethodLookup
{
    public required string Name { get; init; }

    public string? ReceiverType { get; init; }

    public string? Namespace { get; init; }

    public string? ProjectId { get; init; }

    public int? ArgumentCount { get; init; }

    public ImmutableArray<string?> ArgumentTypes { get; init; } = [];

    public ImmutableArray<string> Imports { get; init; } = [];
}

/// <summary>
/// A name-indexed, cross-project view of every symbol a run discovered, regardless of whether that
/// symbol's semantic binding succeeded. Every lookup is a direct key lookup - never a scan of all
/// symbols - and every list-returning lookup is ordered by <see cref="SymbolFactId"/> ordinal so
/// results do not depend on the order facts were supplied in.
/// </summary>
internal sealed class SymbolIndex : ISymbolIndex
{
    private const string MethodKind = "method";

    private readonly FrozenDictionary<SymbolFactId, SymbolFact> _byId;
    private readonly FrozenDictionary<string, ImmutableArray<SymbolFact>> _byName;
    private readonly FrozenDictionary<string, ImmutableArray<SymbolFact>> _byQualifiedName;
    private readonly FrozenDictionary<(string ContainingType, string Name), ImmutableArray<SymbolFact>> _byMember;

    internal SymbolIndex(
        FrozenDictionary<SymbolFactId, SymbolFact> byId,
        FrozenDictionary<string, ImmutableArray<SymbolFact>> byName,
        FrozenDictionary<string, ImmutableArray<SymbolFact>> byQualifiedName,
        FrozenDictionary<(string ContainingType, string Name), ImmutableArray<SymbolFact>> byMember)
    {
        _byId = byId;
        _byName = byName;
        _byQualifiedName = byQualifiedName;
        _byMember = byMember;
    }

    /// <summary>Every indexed symbol, ordered by <see cref="SymbolFactId"/> ordinal.</summary>
    public ImmutableArray<SymbolFact> Symbols =>
        _byId.Values.OrderBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal).ToImmutableArray();

    /// <summary>The symbol carrying this exact identity, or <c>null</c> when the index has none.</summary>
    public SymbolFact? GetById(SymbolFactId id) => _byId.GetValueOrDefault(id);

    /// <summary>
    /// Every indexed type or member whose <see cref="SymbolFact.Name"/> equals <paramref name="simpleName"/>,
    /// across every project. Zero matches is a valid, empty outcome - never <c>null</c>, never a throw.
    /// </summary>
    public ImmutableArray<SymbolFact> FindByName(string simpleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(simpleName);
        return _byName.GetValueOrDefault(simpleName, []);
    }

    /// <summary>
    /// Every indexed symbol whose qualified name matches, compared in
    /// <see cref="TypeNameNormalizer"/>'s normalized form rather than by raw source spelling.
    /// </summary>
    public ImmutableArray<SymbolFact> FindByQualifiedName(string fullyQualifiedName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedName);
        return _byQualifiedName.GetValueOrDefault(TypeNameNormalizer.Normalize(fullyQualifiedName), []);
    }

    /// <summary>
    /// Every indexed member named <paramref name="memberName"/> declared in
    /// <paramref name="containingType"/>, regardless of that member's own resolution level. The
    /// containing type may be given either fully qualified or as its simple name.
    /// </summary>
    public ImmutableArray<SymbolFact> FindMembers(string containingType, string memberName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containingType);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);
        return _byMember.GetValueOrDefault((TypeNameNormalizer.Normalize(containingType), memberName), []);
    }

    /// <summary>
    /// Every method matching <paramref name="lookup"/>, ranked best-first. A candidate whose
    /// declared parameter types match the looked-up argument types outranks a same-count candidate
    /// whose types do not, but neither is dropped: the caller sees both and decides.
    /// </summary>
    public ImmutableArray<SymbolFact> FindMethods(MethodLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var candidates = string.IsNullOrWhiteSpace(lookup.ReceiverType)
            ? FindByName(lookup.Name)
            : FindMembers(lookup.ReceiverType, lookup.Name);

        var methods = candidates.Where(static symbol => string.Equals(symbol.SymbolKind, MethodKind, StringComparison.Ordinal));
        if (lookup.ArgumentCount is { } argumentCount)
        {
            methods = methods.Where(symbol => symbol.ParameterTypes.Length == argumentCount);
        }

        return methods
            .OrderByDescending(symbol => ArgumentTypeMatchScore(symbol, lookup.ArgumentTypes))
            .ThenBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// How many looked-up argument types match the method's declared parameter type at the same
    /// position, compared in normalized form. An argument whose type the caller could not determine
    /// (<c>null</c>) neither matches nor penalizes.
    /// </summary>
    private static int ArgumentTypeMatchScore(SymbolFact method, ImmutableArray<string?> argumentTypes)
    {
        if (argumentTypes.IsDefaultOrEmpty)
        {
            return 0;
        }

        var score = 0;
        var comparable = Math.Min(argumentTypes.Length, method.ParameterTypes.Length);
        for (var position = 0; position < comparable; position++)
        {
            if (Normalized(argumentTypes[position]) is { } argument
                && Normalized(method.ParameterTypes[position]) is { } parameter
                && string.Equals(argument, parameter, StringComparison.Ordinal))
            {
                score++;
            }
        }

        return score;
    }

    private static string? Normalized(string? typeSpelling) =>
        string.IsNullOrWhiteSpace(typeSpelling) ? null : TypeNameNormalizer.Normalize(typeSpelling);
}

/// <summary>
/// The pure, order-independent construction entry point for <see cref="SymbolIndex"/>. Building an
/// index never throws on inconsistent input: symbols colliding on one identity collapse to the
/// ordinal-first entry, and an empty input yields an empty, queryable index.
/// </summary>
internal static class SymbolIndexBuilder
{
    public static SymbolIndex Build(
        IEnumerable<SymbolFact> symbols,
        IEnumerable<ProjectFact> projects,
        IEnumerable<DocumentFact> documents,
        IEnumerable<TargetFact> targets)
    {
        ArgumentNullException.ThrowIfNull(symbols);
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(targets);

        var distinct = symbols
            .OrderBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal)
            .GroupBy(static symbol => symbol.SymbolId)
            .Select(static group => group.First())
            .ToImmutableArray();

        return new SymbolIndex(
            distinct.ToFrozenDictionary(static symbol => symbol.SymbolId),
            GroupByKey(distinct, static symbol => string.IsNullOrWhiteSpace(symbol.Name) ? [] : [symbol.Name]),
            GroupByKey(distinct, static symbol => QualifiedNameKeys(symbol)),
            distinct
                .SelectMany(static symbol => MemberKeys(symbol).Select(key => (Key: key, Symbol: symbol)))
                .GroupBy(static entry => entry.Key)
                .ToFrozenDictionary(
                    static group => group.Key,
                    static group => group.Select(static entry => entry.Symbol).ToImmutableArray()));
    }

    private static FrozenDictionary<string, ImmutableArray<SymbolFact>> GroupByKey(
        ImmutableArray<SymbolFact> symbols,
        Func<SymbolFact, IEnumerable<string>> keySelector) =>
        symbols
            .SelectMany(symbol => keySelector(symbol).Select(key => (Key: key, Symbol: symbol)))
            .GroupBy(static entry => entry.Key, StringComparer.Ordinal)
            .ToFrozenDictionary(
                static group => group.Key,
                static group => group.Select(static entry => entry.Symbol).ToImmutableArray(),
                StringComparer.Ordinal);

    private static IEnumerable<string> QualifiedNameKeys(SymbolFact symbol) =>
        string.IsNullOrWhiteSpace(symbol.FullyQualifiedName)
            ? []
            : [TypeNameNormalizer.Normalize(symbol.FullyQualifiedName)];

    /// <summary>
    /// A member is reachable both by its containing type's fully qualified name and by that type's
    /// simple name, because a caller resolving a call site usually only knows the receiver's simple
    /// name (spec.md's P1 Independent Test looks up "PaymentsService", not the qualified form).
    /// </summary>
    private static IEnumerable<(string ContainingType, string Name)> MemberKeys(SymbolFact symbol)
    {
        if (symbol.ContainingType is not { } containingType
            || string.IsNullOrWhiteSpace(containingType)
            || string.IsNullOrWhiteSpace(symbol.Name))
        {
            yield break;
        }

        var qualified = TypeNameNormalizer.Normalize(containingType);
        yield return (qualified, symbol.Name);

        var simple = SimpleNameKey(qualified);
        if (!string.Equals(simple, qualified, StringComparison.Ordinal))
        {
            yield return (simple, symbol.Name);
        }
    }

    private static string SimpleNameKey(string normalizedQualifiedName)
    {
        var lastDot = normalizedQualifiedName.LastIndexOf('.');
        return lastDot < 0
            ? normalizedQualifiedName
            : TypeNameNormalizer.Normalize(normalizedQualifiedName[(lastDot + 1)..]);
    }
}
