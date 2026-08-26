using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Classification.Persistence;

/// <summary>
/// Resolves the observation ledger into a <see cref="PersistenceModel"/>. Every correlation rule
/// lives here; the emitter only walks the result. The builder reads the ledger and nothing else, so
/// no Roslyn type reaches classification (AD-004).
/// </summary>
internal static class PersistenceModelBuilder
{
    internal const string DbSetTypePrefix = "global::Microsoft.EntityFrameworkCore.DbSet<";
    internal const string ContextTypeKey = "context-type";
    internal const string ConfigurationKeyKey = "key";
    internal const string EntityTypeKey = "entity-type";
    internal const string TableNameKey = "table-name";

    /// <summary>PK-17: the honest literal for a schema no code states. Empty text is not constructible.</summary>
    internal const string UnknownSchema = "unknown";

    private const string GlobalPrefix = "global::";

    /// <summary>The signature components that can name another type, decoded before matching.</summary>
    private static readonly string[] SignatureComponents = ["container", "metadata", "type", "parameters", "type-arguments"];

    public static PersistenceModel Build(ClassifierContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        return new PersistenceModel(ResolveStores(context), [], new CoverageCounts(0, 0, []));
    }

    /// <summary>
    /// One store per <c>DbContext</c>-derived type the ledger names, found as the container of a
    /// <c>DbSet&lt;T&gt;</c> property or as a <c>context-type</c> payload entry (PK-10). A project
    /// naming no context contributes none (PK-13). Stores come out ordinal-sorted by context type.
    /// </summary>
    private static ImmutableArray<StoreNode> ResolveStores(ClassifierContext context)
    {
        var symbols = context.FactsByType<Symbol>();
        var contextTypes = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var property in DbSetProperties(symbols))
        {
            if (SignatureReader.Container(property) is { } container)
            {
                contextTypes.Add(container);
            }
        }

        foreach (var access in context.ObservationsByKind(ObservationKind.DataAccess))
        {
            if (PayloadReader.Value(access, ContextTypeKey) is { } contextType)
            {
                contextTypes.Add(contextType);
            }
        }

        if (contextTypes.Count == 0)
        {
            return [];
        }

        var references = ReferencesByOwner(context, symbols);
        var entityMappings = ResolveEntityMappings(context);
        var typeSymbols = TypeSymbolsByFullyQualifiedName(symbols);
        var observationsBySymbol = ObservationsBySymbol(context);
        return
        [
            .. contextTypes.Select(contextType => new StoreNode(
                contextType,
                DataStoreTechnology.Relational,
                ResolveStoreName(context, contextType, references),
                ResolveObjects(contextType, symbols, entityMappings, typeSymbols, observationsBySymbol))),
        ];
    }

    /// <summary>
    /// One <see cref="ObjectNode"/> per <c>DbSet&lt;T&gt;</c> member the store exposes, form
    /// <c>table</c> (PK-14). A proven <c>ToTable</c> supplies the physical name and
    /// <c>ExplicitConfirmation</c> (PK-15); everything else keeps the member name as a
    /// <c>ConventionalCandidate</c> (PK-16). The schema stays the literal <c>unknown</c> until one is
    /// proven (PK-17). Two members exposing the same entity type stay two objects - the entity type
    /// is never a merge key (PK-20).
    /// </summary>
    private static ImmutableArray<ObjectNode> ResolveObjects(
        string contextTypeFqn,
        ImmutableArray<Symbol> symbols,
        IReadOnlyDictionary<string, EntityMapping> entityMappings,
        IReadOnlyDictionary<string, FactReference> typeSymbols,
        IReadOnlyDictionary<string, List<ObservationIdentity>> observationsBySymbol)
    {
        var objects = new List<ObjectNode>();
        foreach (var property in DbSetProperties(symbols))
        {
            if (!string.Equals(SignatureReader.Container(property), contextTypeFqn, StringComparison.Ordinal)
                || SignatureReader.SoleTypeArgument(SignatureReader.Type(property)) is not { } entityTypeFqn
                || SignatureReader.Metadata(property) is not { } memberName)
            {
                continue;
            }

            entityMappings.TryGetValue(entityTypeFqn, out var mapping);
            var proven = mapping?.TableName;
            var evidence = new List<ObservationIdentity>(mapping?.Evidence ?? []);
            if (observationsBySymbol.TryGetValue(property.Reference.Id.Value, out var declared))
            {
                evidence.AddRange(declared);
            }

            objects.Add(
                new ObjectNode(
                    entityTypeFqn,
                    DataObjectForm.Table,
                    UnknownSchema,
                    proven ?? memberName,
                    proven is null ? MappingStateKind.ConventionalCandidate : MappingStateKind.ExplicitConfirmation,
                    typeSymbols.TryGetValue(entityTypeFqn, out var clrSymbol) ? clrSymbol : null,
                    [],
                    [],
                    Chain(evidence)));
        }

        return
        [
            .. objects
                .OrderBy(static node => node.TableName, StringComparer.Ordinal)
                .ThenBy(static node => node.EntityTypeFqn ?? string.Empty, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// The physical table each entity type is explicitly mapped to, from the <c>table-name</c> plus
    /// <c>entity-type</c> pair a <c>ToTable</c> invocation writes into the ledger. A non-constant
    /// argument leaves <c>table-name</c> out of the payload, which is what makes PK-16's convention
    /// fallback fire. Several mappings for one entity resolve to the ordinal-first name.
    /// </summary>
    private static IReadOnlyDictionary<string, EntityMapping> ResolveEntityMappings(ClassifierContext context)
    {
        var names = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var evidence = new Dictionary<string, List<ObservationIdentity>>(StringComparer.Ordinal);
        foreach (var invocation in context.ObservationsByKind(ObservationKind.Invocation))
        {
            if (PayloadReader.Value(invocation, TableNameKey) is not { } tableName
                || PayloadReader.Value(invocation, EntityTypeKey) is not { } entityTypeFqn)
            {
                continue;
            }

            if (!names.TryGetValue(entityTypeFqn, out var candidates))
            {
                candidates = new SortedSet<string>(StringComparer.Ordinal);
                names[entityTypeFqn] = candidates;
                evidence[entityTypeFqn] = [];
            }

            candidates.Add(tableName);
            evidence[entityTypeFqn].Add(invocation.Identity);
        }

        return names.ToDictionary(
            static pair => pair.Key,
            pair => new EntityMapping(pair.Value.Min!, evidence[pair.Key]),
            StringComparer.Ordinal);
    }

    /// <summary>Named-type <see cref="Symbol"/> facts by fully-qualified name - the CLR side of <c>maps-to</c>.</summary>
    private static IReadOnlyDictionary<string, FactReference> TypeSymbolsByFullyQualifiedName(ImmutableArray<Symbol> symbols)
    {
        var references = new Dictionary<string, FactReference>(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            if (string.Equals(SignatureReader.Kind(symbol), "namedtype", StringComparison.Ordinal)
                && SignatureReader.Type(symbol) is { } typeName
                && !references.ContainsKey(typeName))
            {
                references[typeName] = symbol.Reference;
            }
        }

        return references;
    }

    /// <summary>The observations each symbol owns, so a declaration can anchor an evidence chain.</summary>
    private static IReadOnlyDictionary<string, List<ObservationIdentity>> ObservationsBySymbol(ClassifierContext context)
    {
        var owned = new Dictionary<string, List<ObservationIdentity>>(StringComparer.Ordinal);
        foreach (var observation in context.Observations)
        {
            var ownerId = observation.Identity.Owner.Id.Value;
            if (!owned.TryGetValue(ownerId, out var identities))
            {
                identities = [];
                owned[ownerId] = identities;
            }

            identities.Add(observation.Identity);
        }

        return owned;
    }

    /// <summary>
    /// An evidence chain over the distinct identities, or the uninitialized chain when nothing in the
    /// ledger backs the node - <see cref="Csharp2Md.Domain.Proof.EvidenceChain.Create"/> refuses an
    /// empty chain, and a node resting on a <see cref="Symbol"/> fact alone has no observation.
    /// </summary>
    private static EvidenceChain Chain(IEnumerable<ObservationIdentity> identities)
    {
        var ordered = identities.Distinct().ToArray();
        return ordered.Length == 0 ? default : EvidenceChain.Create(ordered);
    }

    /// <summary>A proven physical table name for one entity type and the invocations that prove it.</summary>
    private sealed record EntityMapping(string TableName, IReadOnlyList<ObservationIdentity> Evidence);

    /// <summary>
    /// The proven connection-string key when a <c>Configuration</c> observation's owning callable
    /// also references the context type (PK-11), and the context's fully-qualified type name
    /// otherwise (PK-12). Several qualifying keys resolve to the ordinal-first, so the name never
    /// depends on observation order.
    /// </summary>
    private static string ResolveStoreName(
        ClassifierContext context,
        string contextTypeFqn,
        IReadOnlyDictionary<string, List<string>> referencesByOwner)
    {
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var configuration in context.ObservationsByKind(ObservationKind.Configuration))
        {
            var key = PayloadReader.Value(configuration, ConfigurationKeyKey);
            if (key is null)
            {
                continue;
            }

            var ownerId = configuration.Identity.Owner.Id.Value;
            if (referencesByOwner.TryGetValue(ownerId, out var references)
                && references.Any(reference => reference.Contains(contextTypeFqn, StringComparison.Ordinal)))
            {
                keys.Add(key);
            }
        }

        return keys.Count > 0 ? keys.Min! : Unqualified(contextTypeFqn);
    }

    /// <summary>
    /// Everything a callable is proven to name: its own canonical signature plus every payload
    /// literal of the observations it owns. That is the ledger's whole record of what a callable
    /// references, which is what PK-11 asks about.
    /// </summary>
    private static IReadOnlyDictionary<string, List<string>> ReferencesByOwner(
        ClassifierContext context,
        ImmutableArray<Symbol> symbols)
    {
        var references = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var symbol in symbols)
        {
            references[symbol.Reference.Id.Value] =
            [
                .. SignatureComponents
                    .Select(component => SignatureReader.Field(symbol.Signature.Value, component))
                    .OfType<string>(),
            ];
        }

        foreach (var observation in context.Observations)
        {
            var ownerId = observation.Identity.Owner.Id.Value;
            if (!references.TryGetValue(ownerId, out var owned))
            {
                owned = [];
                references[ownerId] = owned;
            }

            foreach (var entry in observation.Identity.Payload.Entries)
            {
                if (entry.Value.Value is { } value)
                {
                    owned.Add(value);
                }
            }
        }

        return references;
    }

    /// <summary>The <c>DbSet&lt;T&gt;</c> properties a context exposes - never a method returning one.</summary>
    internal static IEnumerable<Symbol> DbSetProperties(IEnumerable<Symbol> symbols) =>
        symbols.Where(static symbol =>
            string.Equals(SignatureReader.Kind(symbol), "property", StringComparison.Ordinal)
            && SignatureReader.Type(symbol) is { } type
            && type.StartsWith(DbSetTypePrefix, StringComparison.Ordinal));

    /// <summary>
    /// A fully-qualified name without Roslyn's <c>global::</c> alias qualifier, which is display
    /// syntax rather than part of the name PK-12 asks for.
    /// </summary>
    internal static string Unqualified(string fullyQualifiedName) =>
        fullyQualifiedName.StartsWith(GlobalPrefix, StringComparison.Ordinal)
            ? fullyQualifiedName[GlobalPrefix.Length..]
            : fullyQualifiedName;
}
