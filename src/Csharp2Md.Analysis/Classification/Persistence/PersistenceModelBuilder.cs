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
    internal const string SqlTargetKey = "sql-target";
    internal const string SqlOperationKey = "sql-operation";
    internal const string SqlColumnsKey = "sql-columns";
    internal const string FieldNamesKey = "field-names";
    internal const string FieldNameKey = "field-name";
    internal const string PropertyNameKey = "property-name";
    internal const string OperationKey = "operation";
    internal const string ExecuteOperation = "execute";
    internal const string UnknownOperation = "unknown";

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
                ResolveObjects(context, contextType, symbols, entityMappings, typeSymbols, observationsBySymbol))),
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
        ClassifierContext context,
        string contextTypeFqn,
        ImmutableArray<Symbol> symbols,
        IReadOnlyDictionary<string, EntityMapping> entityMappings,
        IReadOnlyDictionary<string, FactReference> typeSymbols,
        IReadOnlyDictionary<string, List<ObservationIdentity>> observationsBySymbol)
    {
        var drafts = new List<ObjectDraft>();
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
            var draft = new ObjectDraft
            {
                EntityTypeFqn = entityTypeFqn,
                Form = DataObjectForm.Table,
                TableName = proven ?? memberName,
                MappingState = proven is null ? MappingStateKind.ConventionalCandidate : MappingStateKind.ExplicitConfirmation,
                ClrSymbol = typeSymbols.TryGetValue(entityTypeFqn, out var clrSymbol) ? clrSymbol : null,
            };
            draft.Evidence.AddRange(mapping?.Evidence ?? []);
            if (observationsBySymbol.TryGetValue(property.Reference.Id.Value, out var declared))
            {
                draft.Evidence.AddRange(declared);
            }

            drafts.Add(draft);
        }

        drafts.AddRange(ResolveSqlObjects(context, contextTypeFqn, symbols));
        ResolveFields(context, contextTypeFqn, drafts, symbols);

        return
        [
            .. drafts
                .OrderBy(static draft => draft.TableName, StringComparer.Ordinal)
                .ThenBy(static draft => draft.EntityTypeFqn ?? string.Empty, StringComparer.Ordinal)
                .Select(static draft => draft.ToNode()),
        ];
    }

    /// <summary>
    /// Fills each object's columns. Three sources feed them: the <c>field-names</c> a LINQ operator
    /// reached (PK-22), an <c>Assignment</c> whose owning callable also flushes with
    /// <c>SaveChanges</c> (PK-23), and a statement's <c>sql-columns</c> list (PK-24). A proven
    /// <c>HasColumnName</c> upgrades the property to its physical name and
    /// <c>ExplicitConfirmation</c> (PK-25); everything else keeps the CLR property name as a
    /// <c>ConventionalCandidate</c> (PK-26). A <c>SELECT *</c> writes no <c>sql-columns</c> entry, so
    /// it contributes no field (PK-27).
    /// </summary>
    private static void ResolveFields(
        ClassifierContext context,
        string contextTypeFqn,
        List<ObjectDraft> drafts,
        ImmutableArray<Symbol> symbols)
    {
        var contextByEntity = ContextTypeByEntityType(symbols);
        var columnMappings = ResolveColumnMappings(context);
        var propertySymbols = PropertySymbolsByDeclaration(symbols);
        var entityObjects = drafts
            .Where(static draft => draft.EntityTypeFqn is not null)
            .ToLookup(static draft => draft.EntityTypeFqn!, StringComparer.Ordinal);
        var sqlObjects = drafts
            .Where(static draft => draft.EntityTypeFqn is null)
            .ToDictionary(static draft => draft.TableName, StringComparer.Ordinal);

        var flushingCallables = new HashSet<string>(StringComparer.Ordinal);
        foreach (var access in context.ObservationsByKind(ObservationKind.DataAccess))
        {
            if (!string.Equals(ExecutingContext(access, contextByEntity), contextTypeFqn, StringComparison.Ordinal))
            {
                continue;
            }

            if (IsFlush(access))
            {
                flushingCallables.Add(access.Identity.Owner.Id.Value);
            }

            if (PayloadReader.Value(access, EntityTypeKey) is { } entityTypeFqn)
            {
                foreach (var name in PayloadReader.Multi(access, FieldNamesKey))
                {
                    foreach (var draft in entityObjects[entityTypeFqn])
                    {
                        AddEntityField(draft, entityTypeFqn, name, access.Identity, columnMappings, propertySymbols);
                    }
                }
            }

            if (PayloadReader.Value(access, SqlTargetKey) is { } target && sqlObjects.TryGetValue(target, out var sqlDraft))
            {
                foreach (var column in PayloadReader.Multi(access, SqlColumnsKey))
                {
                    AddSqlField(sqlDraft, column, access.Identity);
                }
            }
        }

        foreach (var assignment in context.ObservationsByKind(ObservationKind.Assignment))
        {
            if (!flushingCallables.Contains(assignment.Identity.Owner.Id.Value)
                || PayloadReader.Value(assignment, EntityTypeKey) is not { } entityTypeFqn
                || PayloadReader.Value(assignment, FieldNameKey) is not { } propertyName)
            {
                continue;
            }

            foreach (var draft in entityObjects[entityTypeFqn])
            {
                AddEntityField(draft, entityTypeFqn, propertyName, assignment.Identity, columnMappings, propertySymbols);
            }
        }
    }

    /// <summary>
    /// A flush - <c>SaveChanges</c> or <c>SaveChangesAsync</c> - reaches the ledger as a data access
    /// on the context itself with no resolved operation and no statement. It mints nothing on its
    /// own; it is what confirms the same callable's tracked assignments (PK-23).
    /// </summary>
    private static bool IsFlush(Observation access) =>
        PayloadReader.Value(access, ContextTypeKey) is not null
        && PayloadReader.Value(access, SqlTargetKey) is null
        && string.Equals(PayloadReader.Value(access, OperationKey), UnknownOperation, StringComparison.Ordinal);

    private static void AddEntityField(
        ObjectDraft draft,
        string entityTypeFqn,
        string propertyName,
        ObservationIdentity evidence,
        IReadOnlyDictionary<(string EntityTypeFqn, string PropertyName), ColumnMapping> columnMappings,
        IReadOnlyDictionary<(string ContainerFqn, string MemberName), FactReference> propertySymbols)
    {
        if (!draft.Fields.TryGetValue(propertyName, out var field))
        {
            columnMappings.TryGetValue((entityTypeFqn, propertyName), out var mapping);
            field = new FieldDraft
            {
                PropertyName = propertyName,
                FieldName = mapping?.FieldName ?? propertyName,
                MappingState = mapping is null ? MappingStateKind.ConventionalCandidate : MappingStateKind.ExplicitConfirmation,
                ClrSymbol = propertySymbols.TryGetValue((entityTypeFqn, propertyName), out var symbol) ? symbol : null,
            };
            field.Evidence.AddRange(mapping?.Evidence ?? []);
            draft.Fields[propertyName] = field;
        }

        field.Evidence.Add(evidence);
    }

    private static void AddSqlField(ObjectDraft draft, string column, ObservationIdentity evidence)
    {
        if (!draft.Fields.TryGetValue(column, out var field))
        {
            field = new FieldDraft { PropertyName = null, FieldName = column, MappingState = MappingStateKind.ConventionalCandidate };
            draft.Fields[column] = field;
        }

        field.Evidence.Add(evidence);
    }

    /// <summary>
    /// The physical column each entity property is explicitly mapped to, from the <c>field-name</c>,
    /// <c>entity-type</c> and <c>property-name</c> trio a <c>HasColumnName</c> invocation writes into
    /// the ledger. A non-constant argument leaves <c>field-name</c> out, so PK-26's convention
    /// fallback fires. Several mappings for one property resolve to the ordinal-first name.
    /// </summary>
    private static IReadOnlyDictionary<(string EntityTypeFqn, string PropertyName), ColumnMapping> ResolveColumnMappings(
        ClassifierContext context)
    {
        var names = new Dictionary<(string, string), SortedSet<string>>();
        var evidence = new Dictionary<(string, string), List<ObservationIdentity>>();
        foreach (var invocation in context.ObservationsByKind(ObservationKind.Invocation))
        {
            if (PayloadReader.Value(invocation, FieldNameKey) is not { } fieldName
                || PayloadReader.Value(invocation, EntityTypeKey) is not { } entityTypeFqn
                || PayloadReader.Value(invocation, PropertyNameKey) is not { } propertyName)
            {
                continue;
            }

            var key = (entityTypeFqn, propertyName);
            if (!names.TryGetValue(key, out var candidates))
            {
                candidates = new SortedSet<string>(StringComparer.Ordinal);
                names[key] = candidates;
                evidence[key] = [];
            }

            candidates.Add(fieldName);
            evidence[key].Add(invocation.Identity);
        }

        return names.ToDictionary(
            static pair => pair.Key,
            pair => new ColumnMapping(pair.Value.Min!, evidence[pair.Key]));
    }

    /// <summary>Property <see cref="Symbol"/> facts by declaring type and member name - the CLR side of a field's <c>maps-to</c>.</summary>
    private static IReadOnlyDictionary<(string ContainerFqn, string MemberName), FactReference> PropertySymbolsByDeclaration(
        ImmutableArray<Symbol> symbols)
    {
        var references = new Dictionary<(string, string), FactReference>();
        foreach (var symbol in symbols)
        {
            if (string.Equals(SignatureReader.Kind(symbol), "property", StringComparison.Ordinal)
                && SignatureReader.Container(symbol) is { } container
                && SignatureReader.Metadata(symbol) is { } metadata
                && !references.ContainsKey((container, metadata)))
            {
                references[(container, metadata)] = symbol.Reference;
            }
        }

        return references;
    }

    /// <summary>A proven physical column name for one entity property and the invocations that prove it.</summary>
    private sealed record ColumnMapping(string FieldName, IReadOnlyList<ObservationIdentity> Evidence);

    /// <summary>One data object as it accumulates evidence, fields and operations across the ledger.</summary>
    private sealed class ObjectDraft
    {
        public string? EntityTypeFqn { get; init; }

        public DataObjectForm Form { get; init; }

        public string TableName { get; init; } = string.Empty;

        public MappingStateKind MappingState { get; init; }

        public FactReference? ClrSymbol { get; init; }

        public List<ObservationIdentity> Evidence { get; } = [];

        public Dictionary<string, FieldDraft> Fields { get; } = new(StringComparer.Ordinal);

        public ObjectNode ToNode() =>
            new(EntityTypeFqn,
                Form,
                UnknownSchema,
                TableName,
                MappingState,
                ClrSymbol,
                [
                    .. Fields.Values
                        .OrderBy(static field => field.FieldName, StringComparer.Ordinal)
                        .ThenBy(static field => field.PropertyName ?? string.Empty, StringComparer.Ordinal)
                        .Select(static field => field.ToNode()),
                ],
                [],
                Chain(Evidence));
    }

    /// <summary>One column as it accumulates evidence across the occurrences that reach it.</summary>
    private sealed class FieldDraft
    {
        public string? PropertyName { get; init; }

        public string FieldName { get; init; } = string.Empty;

        public MappingStateKind MappingState { get; init; }

        public FactReference? ClrSymbol { get; init; }

        public List<ObservationIdentity> Evidence { get; } = [];

        public FieldNode ToNode() => new(PropertyName, FieldName, MappingState, ClrSymbol, Chain(Evidence));
    }

    /// <summary>
    /// One <see cref="ObjectNode"/> per distinct <c>sql-target</c> the store executes, always a
    /// <c>ConventionalCandidate</c> - a statement names its table, it does not configure one (PK-18).
    /// A target every occurrence executes is a procedure, so its form is <c>unknown</c> (PK-21). SQL
    /// objects are never folded into an entity-set object: similarity, a shared prefix and
    /// pluralization are not evidence (PK-20). Two statements naming one target share one object.
    /// </summary>
    private static IEnumerable<ObjectDraft> ResolveSqlObjects(
        ClassifierContext context,
        string contextTypeFqn,
        ImmutableArray<Symbol> symbols)
    {
        var contextByEntity = ContextTypeByEntityType(symbols);
        var drafts = new SortedDictionary<string, SqlObjectDraft>(StringComparer.Ordinal);
        foreach (var access in context.ObservationsByKind(ObservationKind.DataAccess))
        {
            if (PayloadReader.Value(access, SqlTargetKey) is not { } target
                || !string.Equals(ExecutingContext(access, contextByEntity), contextTypeFqn, StringComparison.Ordinal))
            {
                continue;
            }

            if (!drafts.TryGetValue(target, out var draft))
            {
                draft = new SqlObjectDraft();
                drafts[target] = draft;
            }

            draft.Evidence.Add(access.Identity);
            draft.EveryOccurrenceExecutes &=
                string.Equals(PayloadReader.Value(access, SqlOperationKey), ExecuteOperation, StringComparison.Ordinal);
        }

        return drafts.Select(static pair =>
        {
            var draft = new ObjectDraft
            {
                EntityTypeFqn = null,
                Form = pair.Value.EveryOccurrenceExecutes ? DataObjectForm.Unknown : DataObjectForm.Table,
                TableName = pair.Key,
                MappingState = MappingStateKind.ConventionalCandidate,
                ClrSymbol = null,
            };
            draft.Evidence.AddRange(pair.Value.Evidence);
            return draft;
        });
    }

    /// <summary>
    /// The context a data-access occurrence executes against: its own <c>context-type</c> entry, or
    /// the context exposing the entity set the statement runs on when the receiver was a
    /// <c>DbSet&lt;T&gt;</c> rather than the context itself.
    /// </summary>
    private static string? ExecutingContext(Observation access, IReadOnlyDictionary<string, string> contextTypeByEntityType)
    {
        if (PayloadReader.Value(access, ContextTypeKey) is { } contextType)
        {
            return contextType;
        }

        return PayloadReader.Value(access, EntityTypeKey) is { } entityTypeFqn
            && contextTypeByEntityType.TryGetValue(entityTypeFqn, out var owning)
                ? owning
                : null;
    }

    /// <summary>The context exposing each entity type, ordinal-first when several expose it.</summary>
    private static IReadOnlyDictionary<string, string> ContextTypeByEntityType(ImmutableArray<Symbol> symbols)
    {
        var contexts = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var property in DbSetProperties(symbols))
        {
            if (SignatureReader.SoleTypeArgument(SignatureReader.Type(property)) is not { } entityTypeFqn
                || SignatureReader.Container(property) is not { } container)
            {
                continue;
            }

            if (!contexts.TryGetValue(entityTypeFqn, out var candidates))
            {
                candidates = new SortedSet<string>(StringComparer.Ordinal);
                contexts[entityTypeFqn] = candidates;
            }

            candidates.Add(container);
        }

        return contexts.ToDictionary(static pair => pair.Key, static pair => pair.Value.Min!, StringComparer.Ordinal);
    }

    /// <summary>One SQL statement target as it accumulates across the occurrences naming it.</summary>
    private sealed class SqlObjectDraft
    {
        public List<ObservationIdentity> Evidence { get; } = [];

        public bool EveryOccurrenceExecutes { get; set; } = true;
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
