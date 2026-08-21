using System.Collections.Frozen;
using System.Globalization;
using Csharp2Md.Core.Analysis.Semantics;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
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

    ImmutableArray<SymbolFact> FindMembers(string containingType);

    MethodLookupResult FindMethods(MethodLookup lookup);

    SymbolLookupResult FindCandidates(SymbolLookup lookup);

    ImmutableArray<AnalysisDiagnostic> Diagnostics { get; }

    SymbolIndexMetrics Metrics { get; }
}

/// <summary>
/// A display/filter-only classification of <see cref="SymbolFact.SymbolKind"/>. Kinds the spec does
/// not name individually land in <see cref="Other"/> rather than being dropped, so every indexed
/// symbol still counts exactly once.
/// </summary>
public enum IndexedSymbolKind
{
    Namespace,
    Class,
    Struct,
    RecordClass,
    RecordStruct,
    Interface,
    Enum,
    Delegate,
    Constructor,
    Method,
    Property,
    Field,
    Event,
    Other,
}

/// <summary>
/// The pure mapping from the wire string <c>SyntaxFactExtractor.DeclarationKind</c> produces onto
/// <see cref="IndexedSymbolKind"/>. Total by construction: an unrecognised kind is
/// <see cref="IndexedSymbolKind.Other"/>, never an error and never a dropped symbol.
/// </summary>
internal static class IndexedSymbolKindMap
{
    public static IndexedSymbolKind From(string symbolKind) => symbolKind switch
    {
        "namespace" => IndexedSymbolKind.Namespace,
        "class" => IndexedSymbolKind.Class,
        "struct" => IndexedSymbolKind.Struct,
        "record" => IndexedSymbolKind.RecordClass,
        "record-struct" => IndexedSymbolKind.RecordStruct,
        "interface" => IndexedSymbolKind.Interface,
        "enum" => IndexedSymbolKind.Enum,
        "delegate" => IndexedSymbolKind.Delegate,
        "constructor" => IndexedSymbolKind.Constructor,
        "method" => IndexedSymbolKind.Method,
        "property" => IndexedSymbolKind.Property,
        "field" => IndexedSymbolKind.Field,
        "event" => IndexedSymbolKind.Event,
        _ => IndexedSymbolKind.Other,
    };
}

/// <summary>Summary counts describing what an index ended up containing.</summary>
public sealed record SymbolIndexMetrics(
    int TotalSymbols,
    ImmutableDictionary<FactResolution, int> ByResolution,
    ImmutableDictionary<IndexedSymbolKind, int> ByKind,
    int DuplicateIdCount,
    int AmbiguousSimpleNameCount);

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
/// The query shape for <see cref="ISymbolIndex.FindCandidates"/>. Every property beyond
/// <see cref="Name"/> is a contextual hint that promotes a candidate up the priority sequence; none
/// of them removes a candidate from the result.
/// </summary>
internal sealed record SymbolLookup
{
    public required string Name { get; init; }

    public string? ContainingType { get; init; }

    public string? Namespace { get; init; }

    public string? ProjectId { get; init; }

    public ImmutableArray<string> Imports { get; init; } = [];
}

/// <summary>
/// Whether a lookup landed on exactly one best candidate, on a tie the caller must resolve, or on
/// nothing at all. "Not found" and "ambiguous" stay distinguishable outcomes on purpose.
/// </summary>
internal enum SymbolLookupStatus
{
    Unique,
    Ambiguous,
    NotFound,
}

/// <summary>
/// The outcome of <see cref="ISymbolIndex.FindCandidates"/>: every candidate found, ordered
/// best-first, plus whether the best tier held one candidate or several.
/// </summary>
/// <param name="TiedCandidateCount">
/// How many leading entries of <see cref="Candidates"/> share the best priority tier - <c>1</c> when
/// <see cref="Status"/> is <see cref="SymbolLookupStatus.Unique"/>, <c>0</c> when
/// <see cref="SymbolLookupStatus.NotFound"/>, and 2 or more when
/// <see cref="SymbolLookupStatus.Ambiguous"/>. <see cref="Candidates"/> is sorted tier-then-id, so
/// <c>Candidates[..TiedCandidateCount]</c> is exactly the tied set a consumer like
/// <c>SymbolIndexStrategy</c> (relation-resolver) needs without re-deriving <see cref="SymbolIndex.PriorityTier"/>
/// itself - <see cref="Candidates"/> alone cannot answer "which of these actually tied" once a lookup
/// mixes best-tier and lower-tier results, which every existing caller's `Unique` scenario already does.
/// </param>
internal sealed record SymbolLookupResult(
    SymbolLookupStatus Status, ImmutableArray<SymbolFact> Candidates, int TiedCandidateCount);

/// <summary>
/// The outcome of <see cref="ISymbolIndex.FindMethods"/>: every matching method, ranked best-first by
/// <see cref="MethodLookup.ArgumentTypes"/> match score, plus how many leading entries share that best
/// score - the same "how many actually tied" answer <see cref="SymbolLookupResult.TiedCandidateCount"/>
/// gives for <see cref="ISymbolIndex.FindCandidates"/>, and for the same reason: the match score is
/// computed internally and a caller like <c>ReceiverTypeStrategy</c> (relation-resolver) cannot
/// otherwise tell a genuine tie from an unrelated lower-scored candidate that merely rode along in
/// <see cref="Methods"/>.
/// </summary>
internal sealed record MethodLookupResult(ImmutableArray<SymbolFact> Methods, int TiedCandidateCount);

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
    private readonly FrozenDictionary<string, ImmutableArray<SymbolFact>> _byContainingType;
    private readonly FrozenDictionary<string, SymbolFact> _byIdValue;
    private readonly FrozenDictionary<DocumentFactId, string> _projectByDocument;

    internal SymbolIndex(
        FrozenDictionary<SymbolFactId, SymbolFact> byId,
        FrozenDictionary<string, ImmutableArray<SymbolFact>> byName,
        FrozenDictionary<string, ImmutableArray<SymbolFact>> byQualifiedName,
        FrozenDictionary<(string ContainingType, string Name), ImmutableArray<SymbolFact>> byMember,
        FrozenDictionary<string, ImmutableArray<SymbolFact>> byContainingType,
        FrozenDictionary<string, SymbolFact> byIdValue,
        FrozenDictionary<DocumentFactId, string> projectByDocument,
        ImmutableArray<AnalysisDiagnostic> diagnostics,
        SymbolIndexMetrics metrics)
    {
        Metrics = metrics;
        _byId = byId;
        _byName = byName;
        _byQualifiedName = byQualifiedName;
        _byMember = byMember;
        _byContainingType = byContainingType;
        _byIdValue = byIdValue;
        _projectByDocument = projectByDocument;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// What the build found wrong with the facts it was given. Recording a diagnostic never aborts
    /// the build: an inconsistent input degrades to an entry here and the index stays queryable.
    /// </summary>
    public ImmutableArray<AnalysisDiagnostic> Diagnostics { get; }

    /// <summary>Summary counts for what this index contains, computed once when it was built.</summary>
    public SymbolIndexMetrics Metrics { get; }

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
    /// Every indexed member declared in <paramref name="containingType"/>, ordered by
    /// <see cref="SymbolFactId"/> ordinal. Reachable by the type's fully qualified name or by its
    /// simple name, exactly like the two-argument lookup. Zero members is a valid, empty outcome.
    /// </summary>
    public ImmutableArray<SymbolFact> FindMembers(string containingType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(containingType);
        return _byContainingType.GetValueOrDefault(TypeNameNormalizer.Normalize(containingType), []);
    }

    /// <summary>
    /// Every method matching <paramref name="lookup"/>, ranked best-first. A candidate whose
    /// declared parameter types match the looked-up argument types outranks a same-count candidate
    /// whose types do not, but neither is dropped: the caller sees both and decides.
    /// </summary>
    public MethodLookupResult FindMethods(MethodLookup lookup)
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

        var ranked = methods
            .Select(symbol => (Symbol: symbol, Score: ArgumentTypeMatchScore(symbol, lookup.ArgumentTypes)))
            .OrderByDescending(static entry => entry.Score)
            .ThenBy(static entry => entry.Symbol.SymbolId.Value, StringComparer.Ordinal)
            .ToImmutableArray();

        if (ranked.IsEmpty)
        {
            return new MethodLookupResult([], 0);
        }

        var bestScore = ranked[0].Score;
        var tied = ranked.Count(entry => entry.Score == bestScore);

        return new MethodLookupResult(ranked.Select(static entry => entry.Symbol).ToImmutableArray(), tied);
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

    /// <summary>
    /// Every candidate for <paramref name="lookup"/>, ordered best-first by the priority sequence,
    /// with the status saying whether the best tier held one candidate or several. A tie is never
    /// broken for the caller: it surfaces as <see cref="SymbolLookupStatus.Ambiguous"/> with every
    /// tied candidate still listed.
    /// </summary>
    public SymbolLookupResult FindCandidates(SymbolLookup lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);

        var ranked = CandidatePool(lookup.Name)
            .Select(symbol => (Symbol: symbol, Tier: PriorityTier(symbol, lookup)))
            .OrderBy(static entry => entry.Tier)
            .ThenBy(static entry => entry.Symbol.SymbolId.Value, StringComparer.Ordinal)
            .ToImmutableArray();

        if (ranked.IsEmpty)
        {
            return new SymbolLookupResult(SymbolLookupStatus.NotFound, [], 0);
        }

        var bestTier = ranked[0].Tier;
        var tied = ranked.Count(entry => entry.Tier == bestTier);

        return new SymbolLookupResult(
            tied == 1 ? SymbolLookupStatus.Unique : SymbolLookupStatus.Ambiguous,
            ranked.Select(static entry => entry.Symbol).ToImmutableArray(),
            tied);
    }

    /// <summary>
    /// Everything the looked-up name could plausibly refer to: an exact identity, a qualified name,
    /// or a simple name. Ranking - not pool membership - decides which of them wins.
    /// </summary>
    private ImmutableArray<SymbolFact> CandidatePool(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var pool = new List<SymbolFact>();
        if (_byIdValue.TryGetValue(name, out var byIdentity))
        {
            pool.Add(byIdentity);
        }

        pool.AddRange(_byQualifiedName.GetValueOrDefault(TypeNameNormalizer.Normalize(name), []));
        pool.AddRange(_byName.GetValueOrDefault(name, []));

        return pool.Distinct().ToImmutableArray();
    }

    /// <summary>
    /// Where a candidate sits in spec.md's priority sequence - lower is better: exact id, fully
    /// qualified name, same containing type, same namespace, a namespace the caller imported, same
    /// project, then any project at all.
    /// </summary>
    private int PriorityTier(SymbolFact symbol, SymbolLookup lookup)
    {
        if (string.Equals(symbol.SymbolId.Value, lookup.Name, StringComparison.Ordinal))
        {
            return 0;
        }

        if (Normalized(symbol.FullyQualifiedName) is { } qualified
            && string.Equals(qualified, TypeNameNormalizer.Normalize(lookup.Name), StringComparison.Ordinal))
        {
            return 1;
        }

        if (SameNormalized(symbol.ContainingType, lookup.ContainingType))
        {
            return 2;
        }

        if (SameNormalized(symbol.Namespace, lookup.Namespace))
        {
            return 3;
        }

        if (!lookup.Imports.IsDefaultOrEmpty
            && lookup.Imports.Any(import => SameNormalized(symbol.Namespace, import)))
        {
            return 4;
        }

        if (lookup.ProjectId is { } projectId
            && _projectByDocument.TryGetValue(symbol.DocumentId, out var owning)
            && string.Equals(owning, projectId, StringComparison.Ordinal))
        {
            return 5;
        }

        return 6;
    }

    private static bool SameNormalized(string? left, string? right) =>
        Normalized(left) is { } normalizedLeft
        && Normalized(right) is { } normalizedRight
        && string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
}

/// <summary>
/// The pure, order-independent construction entry point for <see cref="SymbolIndex"/>. Building an
/// index never throws on inconsistent input: symbols colliding on one identity collapse to the
/// ordinal-first entry, and an empty input yields an empty, queryable index.
/// </summary>
internal static class SymbolIndexBuilder
{
    private const string DuplicatedSymbolIdCode = "C2M-SYMIDX-001";
    private const string InvalidContainingSymbolCode = "C2M-SYMIDX-002";
    private const string AmbiguousSymbolLookupCode = "C2M-SYMIDX-003";

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

        var byIdentity = symbols
            .OrderBy(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal)
            .GroupBy(static symbol => symbol.SymbolId)
            .ToImmutableArray();
        var distinct = byIdentity.Select(static group => group.First()).ToImmutableArray();
        var diagnostics = Diagnose(byIdentity, distinct);

        return new SymbolIndex(
            distinct.ToFrozenDictionary(static symbol => symbol.SymbolId),
            GroupByKey(distinct, static symbol => string.IsNullOrWhiteSpace(symbol.Name) ? [] : [symbol.Name]),
            GroupByKey(distinct, static symbol => QualifiedNameKeys(symbol)),
            distinct
                .SelectMany(static symbol => MemberKeys(symbol).Select(key => (Key: key, Symbol: symbol)))
                .GroupBy(static entry => entry.Key)
                .ToFrozenDictionary(
                    static group => group.Key,
                    static group => group.Select(static entry => entry.Symbol).ToImmutableArray()),
            GroupByKey(distinct, static symbol => MemberKeys(symbol).Select(static key => key.ContainingType)),
            distinct.ToFrozenDictionary(static symbol => symbol.SymbolId.Value, StringComparer.Ordinal),
            documents
                .GroupBy(static document => document.DocumentId)
                .ToFrozenDictionary(
                    static group => group.Key,
                    static group => group.First().ProjectId.Value),
            diagnostics,
            Measure(distinct, diagnostics));
    }

    /// <summary>
    /// The summary counts spec.md's P3 criterion 4 asks for, computed once from the same collapsed
    /// symbol set and diagnostic list the index itself was built from.
    /// </summary>
    private static SymbolIndexMetrics Measure(
        ImmutableArray<SymbolFact> distinct,
        ImmutableArray<AnalysisDiagnostic> diagnostics) =>
        new(distinct.Length,
            distinct
                .GroupBy(static symbol => symbol.Header.Resolution)
                .ToImmutableDictionary(static group => group.Key, static group => group.Count()),
            distinct
                .GroupBy(static symbol => IndexedSymbolKindMap.From(symbol.SymbolKind))
                .ToImmutableDictionary(static group => group.Key, static group => group.Count()),
            diagnostics.Count(static diagnostic => diagnostic.Code == DuplicatedSymbolIdCode),
            diagnostics.Count(static diagnostic => diagnostic.Code == AmbiguousSymbolLookupCode));

    /// <summary>
    /// Everything inconsistent about the supplied facts, scanned once here at build-completion time
    /// rather than per query, so ambiguity is visible without a caller having to probe every name.
    /// </summary>
    private static ImmutableArray<AnalysisDiagnostic> Diagnose(
        ImmutableArray<IGrouping<SymbolFactId, SymbolFact>> byIdentity,
        ImmutableArray<SymbolFact> distinct)
    {
        var diagnostics = ImmutableArray.CreateBuilder<AnalysisDiagnostic>();

        foreach (var collision in byIdentity.Where(static group => group.Count() > 1))
        {
            var kept = collision.First();
            diagnostics.Add(Diagnostic(
                DuplicatedSymbolIdCode,
                "duplicated-symbol-id",
                DiagnosticSeverity.Warning,
                kept.Header.Id,
                $"Symbol identity '{kept.SymbolId.Value}' was produced by {collision.Count()} facts; one entry was kept.",
                new DiagnosticData("symbol_id", kept.SymbolId.Value),
                new DiagnosticData("discarded", Count(collision.Count() - 1))));
        }

        var identities = distinct.Select(static symbol => symbol.SymbolId).ToImmutableHashSet();
        foreach (var symbol in distinct)
        {
            if (symbol.ContainingSymbolId is { } containing && !identities.Contains(containing))
            {
                diagnostics.Add(Diagnostic(
                    InvalidContainingSymbolCode,
                    "invalid-containing-symbol",
                    DiagnosticSeverity.Warning,
                    symbol.Header.Id,
                    $"Symbol '{symbol.SymbolId.Value}' references a containing symbol absent from the index.",
                    new DiagnosticData("containing_symbol_id", containing.Value)));
            }
        }

        var ambiguous = distinct
            .Where(static symbol => !string.IsNullOrWhiteSpace(symbol.Name))
            .GroupBy(static symbol => symbol.Name, StringComparer.Ordinal)
            .Where(static group => group
                .Select(static symbol => (symbol.Namespace, symbol.ContainingType))
                .Distinct()
                .Count() > 1);
        foreach (var group in ambiguous)
        {
            var first = group.First();
            diagnostics.Add(Diagnostic(
                AmbiguousSymbolLookupCode,
                "ambiguous-symbol-lookup",
                DiagnosticSeverity.Information,
                first.Header.Id,
                $"Simple name '{group.Key}' resolves to candidates in more than one namespace or containing type.",
                new DiagnosticData("name", group.Key),
                new DiagnosticData("candidates", Count(group.Count()))));
        }

        return [.. diagnostics.Order()];
    }

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static AnalysisDiagnostic Diagnostic(
        string code,
        string rule,
        DiagnosticSeverity severity,
        FactId scopeId,
        string message,
        params DiagnosticData[] data) =>
        AnalysisDiagnostic.Create(
            code,
            severity,
            DiagnosticStage.Projection,
            scopeId,
            message,
            [new DiagnosticData("rule", rule), .. data]);

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
