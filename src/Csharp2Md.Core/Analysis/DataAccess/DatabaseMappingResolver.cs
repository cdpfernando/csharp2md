using System.Collections.Frozen;
using Csharp2Md.Core.Analysis.Indexes;
using Csharp2Md.Core.Analysis.Relations;
using Csharp2Md.Core.Facts.Identity;
using Csharp2Md.Core.Facts.Metadata;
using Csharp2Md.Core.Facts.Model;
using Csharp2Md.Core.Facts.Validation;

namespace Csharp2Md.Core.Analysis.DataAccess;

/// <summary>
/// A database object node whose name at least one source literal proved. Evidence accumulates across
/// every claim that named it, and the strongest contributing resolution is the one that survives.
/// </summary>
internal sealed record ResolvedDatabaseObject(
    DatabaseObjectFactId ObjectId,
    DatabaseObjectKind Kind,
    string Name,
    FactResolution Resolution,
    ImmutableArray<Evidence> Evidence,
    ImmutableArray<DataAccessAnalyzerId> AnalyzerIds);

/// <summary>A column of a <see cref="ResolvedDatabaseObject"/>, on the same proven-name terms.</summary>
internal sealed record ResolvedDatabaseColumn(
    DatabaseColumnFactId ColumnId,
    DatabaseObjectFactId ObjectId,
    string Name,
    FactResolution Resolution,
    ImmutableArray<Evidence> Evidence,
    ImmutableArray<DataAccessAnalyzerId> AnalyzerIds);

/// <summary>
/// Everything pass two decided. <see cref="Objects"/> and <see cref="Columns"/> still become
/// <c>DatabaseFragmentBuilder</c>'s solution-level fragment; <see cref="Relations"/> (RELR-32) are
/// claims for the <c>RelationClaimAccumulator</c>, never facts this record's own fragment carries.
/// </summary>
internal sealed record DatabaseResolution(
    ImmutableArray<ResolvedDatabaseObject> Objects,
    ImmutableArray<ResolvedDatabaseColumn> Columns,
    ImmutableArray<RawRelation> Relations,
    ImmutableArray<DocumentExtent> Documents)
{
    public static DatabaseResolution Empty { get; } = new([], [], [], []);

    /// <summary>
    /// Whether <c>DatabaseFragmentBuilder</c> has nothing left to build - judged on
    /// <see cref="Objects"/>/<see cref="Columns"/> alone now that <see cref="Relations"/> no longer
    /// feeds that fragment (RELR-32).
    /// </summary>
    public bool IsEmpty => Objects.IsEmpty && Columns.IsEmpty;
}

/// <summary>
/// Pass two. Correlates the whole run's claims into entity-to-object and property-to-column maps, then
/// turns every claim into a relation. A node is minted only from a name a literal proved; everything
/// else stays a relation with <c>target_id: null</c>, the observed text, and a non-<c>Exact</c>
/// resolution.
/// </summary>
/// <remarks>
/// Whole-run visibility is what makes this a second pass: the <c>ToTable</c> that configures an entity
/// commonly lives in a different document from the entity, and configured-over-convention precedence
/// (DAD-04) is undecidable until every document has been seen.
/// </remarks>
internal static class DatabaseMappingResolver
{
    internal const string ExposesKind = "exposes";
    internal const string MapsToKind = "maps-to";
    internal const string MapsPropertyToColumnKind = "maps-property-to-column";
    internal const string ReadsKind = "reads";
    internal const string WritesKind = "writes";
    internal const string ExecutesKind = "executes";
    internal const string AccessesKind = "accesses";
    internal const string ReadsColumnKind = "reads-column";
    internal const string WritesColumnKind = "writes-column";
    internal const string FiltersByKind = "filters-by";

    internal const string TargetTextKey = "target_text";
    internal const string MappingKey = "mapping";
    internal const string OperationKey = "operation";
    internal const string UsageKey = "usage";
    internal const string SqlKey = "sql";

    internal const string ConfiguredMapping = "configured";
    internal const string ConventionMapping = "convention";

    /// <summary>DAD-02: a convention name is a claim about a target, never proof one exists.</summary>
    internal const string ConventionMappingReason = "convention-mapping";

    /// <summary>DAD-01: an entity set names a CLR entity, which is not itself a database object.</summary>
    internal const string EntitySetTargetReason = "entity-is-not-a-database-object";

    /// <summary>
    /// A column literal proves a name but not an identity while its entity's table is only a
    /// convention: a column node can never exist without an owning object node.
    /// </summary>
    internal const string UnmappedOwningObjectReason = "unmapped-owning-object";

    /// <summary>DAD-12: the property named several exposed entities, so no single one can be claimed.</summary>
    internal const string AmbiguousEntityReason = "ambiguous-entity-attribution";

    /// <summary>
    /// DAD-14's floor: a relation with no target always states a reason. Only a claim that reached pass
    /// two without one of its own lands here.
    /// </summary>
    internal const string UnresolvedTargetReason = "unresolved-target";

    private const string PropertyDeclarationKind = "property";

    /// <summary>The declaration kinds an entity type can be declared as.</summary>
    private static readonly FrozenSet<string> EntityDeclarationKinds =
        new[] { "class", "record", "record-struct", "struct" }.ToFrozenSet(StringComparer.Ordinal);

    public static DatabaseResolution Resolve(DatabaseClaimSnapshot snapshot, ISymbolIndex symbols)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(symbols);

        var objects = new ObjectCatalogue();
        var columns = new ColumnCatalogue();
        var relations = ImmutableArray.CreateBuilder<RawRelation>();

        // Every configured table is read before any mapping is emitted, which is what makes DAD-04's
        // precedence independent of the order documents were analysed in.
        var objectByEntity = ResolveEntityTables(snapshot, objects);
        var columnByProperty = ResolveEntityColumns(snapshot, objectByEntity, columns);
        EmitEntitySetExposures(snapshot, relations);
        var mappedEntities = EmitEntityMappings(snapshot, symbols, objectByEntity, relations);
        EmitPropertyMappings(snapshot, symbols, mappedEntities, columnByProperty, relations);
        var sqlObjectByStatement = EmitAccesses(snapshot, objectByEntity, objects, relations);
        EmitColumnAccesses(
            snapshot, symbols, columnByProperty, sqlObjectByStatement, columns, relations);

        return new DatabaseResolution(
            objects.ToImmutable(), columns.ToImmutable(), relations.ToImmutable(), snapshot.Documents);
    }

    /// <summary>
    /// Builds one <see cref="RawRelation"/> claim in the <c>Data</c> partition, carrying whatever target
    /// this resolver already proved (RELR-32). RELR-02: when <paramref name="targetId"/> is set, the
    /// claim also reports the method that proved it, derived from <paramref name="resolution"/> - a
    /// source literal (an explicit configuration call, or a SQL statement the reader tokenized) proves
    /// <see cref="ResolutionMethod.Configured"/>, while a single-match tracked-write attribution
    /// (<see cref="EmitTrackedWrite"/>) proves only <see cref="ResolutionMethod.Heuristic"/> - it is
    /// still a real target, but inferred from a unique name match, not read from source. A null target
    /// defers the configured/convention/dynamic classification to <c>DatabaseRelationStrategy</c> (T20),
    /// which reads the <c>mapping</c> detail this claim's <paramref name="details"/> already carries.
    /// </summary>
    private static RawRelation RelationClaim(
        FactId sourceId,
        FactId? targetId,
        string relationKind,
        FactResolution resolution,
        string? unresolvedReason,
        ImmutableArray<RelationDetail> details,
        Evidence evidence) =>
        new()
        {
            Kind = relationKind,
            OwnerId = sourceId,
            Evidence = evidence,
            ShapeConfidence = resolution,
            Partition = RelationPartition.Data,
            Details = details,
            TargetId = targetId,
            ProducerMethod = targetId is null ? null : ProvenTargetMethod(resolution),
            UnresolvedReason = unresolvedReason,
        };

    /// <summary>
    /// <see cref="FactResolution.Heuristic"/> is the only shape confidence a proven (non-null) target
    /// carries that is not a source-literal proof - every other value this resolver assigns to a proven
    /// target (<see cref="FactResolution.Exact"/> for a configuration call, <see cref="FactResolution.Syntactic"/>
    /// for a resolved SQL statement's object) is read from source, so <see cref="ResolutionMethod.Configured"/>.
    /// </summary>
    private static ResolutionMethod ProvenTargetMethod(FactResolution resolution) =>
        resolution == FactResolution.Heuristic ? ResolutionMethod.Heuristic : ResolutionMethod.Configured;

    /// <summary>
    /// DAD-03: one node per configured table literal, and the entity-to-object map every later stage
    /// resolves against. An entity configured twice keeps its first mapping in canonical claim order,
    /// so the map does not depend on which document was analysed first.
    /// </summary>
    private static Dictionary<string, DatabaseObjectFactId> ResolveEntityTables(
        DatabaseClaimSnapshot snapshot,
        ObjectCatalogue objects)
    {
        var objectByEntity = new Dictionary<string, DatabaseObjectFactId>(StringComparer.Ordinal);
        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.TableConfigured
                || claim is not { ObjectText: { } tableName, ObjectKind: { } kind, EntityText: { } entityName })
            {
                continue;
            }

            objectByEntity.TryAdd(entityName, objects.Mint(kind, tableName, FactResolution.Exact, claim));
        }

        return objectByEntity;
    }

    /// <summary>
    /// DAD-01: one <c>exposes</c> per discovered <c>DbSet</c> property, sourced at the declaring context
    /// type and naming the entity as observed text.
    /// </summary>
    private static void EmitEntitySetExposures(
        DatabaseClaimSnapshot snapshot,
        ImmutableArray<RawRelation>.Builder relations)
    {
        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.EntitySetExposed || claim.EntityText is not { } entityName)
            {
                continue;
            }

            relations.Add(RelationClaim(
                claim.OwnerId,
                null,
                ExposesKind,
                claim.ShapeConfidence,
                EntitySetTargetReason,
                [new RelationDetail(TargetTextKey, entityName)],
                claim.Evidence));
        }
    }

    /// <summary>
    /// DAD-05: one column node per configured column literal whose entity resolved to an object node,
    /// plus the property-to-column map every column access resolves against. A configured column on an
    /// entity the run only convention-mapped mints nothing: a column node without an owning object node
    /// would be an identity nothing proved.
    /// </summary>
    private static Dictionary<(string Entity, string Property), DatabaseColumnFactId> ResolveEntityColumns(
        DatabaseClaimSnapshot snapshot,
        Dictionary<string, DatabaseObjectFactId> objectByEntity,
        ColumnCatalogue columns)
    {
        var columnByProperty = new Dictionary<(string Entity, string Property), DatabaseColumnFactId>();
        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.ColumnConfigured
                || claim is not { EntityText: { } entityName, PropertyText: { } propertyName, ColumnText: { } columnName }
                || !objectByEntity.TryGetValue(entityName, out var objectId))
            {
                continue;
            }

            columnByProperty.TryAdd(
                (entityName, propertyName),
                columns.Mint(objectId, columnName, FactResolution.Exact, claim));
        }

        return columnByProperty;
    }

    /// <summary>
    /// DAD-02, DAD-03 and DAD-04: the configured mapping when the run proved one, and otherwise one
    /// convention mapping per distinct <c>DbSet</c> property name the entity was exposed under. Returns
    /// the claim that anchored each mapped entity, which is the evidence a convention column mapping
    /// carries - the entity's own document need not have produced a claim, so it has no retained extent.
    /// </summary>
    private static Dictionary<string, RawDatabaseClaim> EmitEntityMappings(
        DatabaseClaimSnapshot snapshot,
        ISymbolIndex symbols,
        Dictionary<string, DatabaseObjectFactId> objectByEntity,
        ImmutableArray<RawRelation>.Builder relations)
    {
        var mappedEntities = new Dictionary<string, RawDatabaseClaim>(StringComparer.Ordinal);
        var configured = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.TableConfigured
                || claim is not { ObjectText: { } tableName, EntityText: { } entityName }
                || !objectByEntity.TryGetValue(entityName, out var objectId)
                || !configured.Add(entityName)
                || EntitySymbol(symbols, entityName) is not { } source)
            {
                continue;
            }

            relations.Add(RelationClaim(
                source,
                objectId.ToFactId(),
                MapsToKind,
                FactResolution.Exact,
                null,
                [new RelationDetail(TargetTextKey, tableName), new RelationDetail(MappingKey, ConfiguredMapping)],
                claim.Evidence));
            mappedEntities[entityName] = claim;
        }

        var conventions = new HashSet<(string Entity, string Set)>();
        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.EntitySetExposed
                || claim is not { EntityText: { } entityName, PropertyText: { } setName }
                || objectByEntity.ContainsKey(entityName)
                || !conventions.Add((entityName, setName))
                || EntitySymbol(symbols, entityName) is not { } source)
            {
                continue;
            }

            relations.Add(RelationClaim(
                source,
                null,
                MapsToKind,
                FactResolution.Heuristic,
                ConventionMappingReason,
                [new RelationDetail(TargetTextKey, setName), new RelationDetail(MappingKey, ConventionMapping)],
                claim.Evidence));
            mappedEntities.TryAdd(entityName, claim);
        }

        return mappedEntities;
    }

    /// <summary>
    /// DAD-05 and DAD-06: the configured mapping for every property a <c>HasColumnName</c> named, and a
    /// convention mapping naming the property itself for every other property of a mapped entity.
    /// </summary>
    private static void EmitPropertyMappings(
        DatabaseClaimSnapshot snapshot,
        ISymbolIndex symbols,
        Dictionary<string, RawDatabaseClaim> mappedEntities,
        Dictionary<(string Entity, string Property), DatabaseColumnFactId> columnByProperty,
        ImmutableArray<RawRelation>.Builder relations)
    {
        var configured = new HashSet<(string Entity, string Property)>();
        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.ColumnConfigured
                || claim is not { EntityText: { } entityName, PropertyText: { } propertyName, ColumnText: { } columnName }
                || !configured.Add((entityName, propertyName))
                || PropertySymbol(symbols, entityName, propertyName) is not { } source)
            {
                continue;
            }

            var columnId = columnByProperty.TryGetValue((entityName, propertyName), out var resolved)
                ? resolved.ToFactId()
                : (FactId?)null;
            relations.Add(RelationClaim(
                source,
                columnId,
                MapsPropertyToColumnKind,
                columnId is null ? FactResolution.Unresolved : FactResolution.Exact,
                columnId is null ? UnmappedOwningObjectReason : null,
                [new RelationDetail(TargetTextKey, columnName), new RelationDetail(MappingKey, ConfiguredMapping)],
                claim.Evidence));
        }

        foreach (var (entityName, anchor) in mappedEntities.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
        {
            foreach (var property in symbols.FindMembers(entityName)
                .Where(static symbol => symbol.SymbolKind == PropertyDeclarationKind))
            {
                if (configured.Contains((entityName, property.Name)))
                {
                    continue;
                }

                relations.Add(RelationClaim(
                    property.SymbolId.ToFactId(),
                    null,
                    MapsPropertyToColumnKind,
                    FactResolution.Heuristic,
                    ConventionMappingReason,
                    [
                        new RelationDetail(TargetTextKey, property.Name),
                        new RelationDetail(MappingKey, ConventionMapping),
                    ],
                    anchor.Evidence));
            }
        }
    }

    /// <summary>
    /// The single indexed property this entity declares under that name, or <c>null</c> when the run
    /// holds none or several. Same discipline as <see cref="EntitySymbol"/>: no guessed source.
    /// </summary>
    private static FactId? PropertySymbol(ISymbolIndex symbols, string entityName, string propertyName)
    {
        var candidates = symbols.FindMembers(entityName, propertyName)
            .Where(static symbol => symbol.SymbolKind == PropertyDeclarationKind)
            .ToArray();
        return candidates is [var single] ? single.SymbolId.ToFactId() : null;
    }

    /// <summary>
    /// DAD-07, DAD-10, DAD-14 and DAD-21..DAD-23, DAD-27, DAD-28: every access claim becomes a relation
    /// whose coarse kind carries the direction and whose <c>operation</c> detail carries the precise
    /// verb. An EF access resolves through the entity map; a SQL access mints its own node, but only
    /// when the reader proved the object's name - which is exactly what a non-null <c>ObjectKind</c>
    /// records. Returns the object each readable statement resolved to, so its columns can be hung on it.
    /// </summary>
    private static Dictionary<Evidence, DatabaseObjectFactId> EmitAccesses(
        DatabaseClaimSnapshot snapshot,
        Dictionary<string, DatabaseObjectFactId> objectByEntity,
        ObjectCatalogue objects,
        ImmutableArray<RawRelation>.Builder relations)
    {
        var sqlObjectByStatement = new Dictionary<Evidence, DatabaseObjectFactId>();
        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.Access)
            {
                continue;
            }

            if (claim is { EntityText: { } entityName, PropertyText: { } setName })
            {
                var mapped = objectByEntity.TryGetValue(entityName, out var objectId);
                relations.Add(RelationClaim(
                    claim.OwnerId,
                    mapped ? objectId.ToFactId() : null,
                    AccessKind(claim.Operation),
                    mapped ? FactResolution.Exact : FactResolution.Heuristic,
                    mapped ? null : ConventionMappingReason,
                    [
                        new RelationDetail(OperationKey, DatabaseFactWire.Name(claim.Operation)),
                        new RelationDetail(TargetTextKey, setName),
                    ],
                    claim.Evidence));
                continue;
            }

            // DAD-22 and DAD-27: a name the reader proved carries a kind; a placeholder carries none,
            // and mints nothing.
            DatabaseObjectFactId? sqlObjectId = claim is { ObjectText: { } objectName, ObjectKind: { } kind }
                ? objects.Mint(kind, objectName, FactResolution.Exact, claim)
                : null;
            if (sqlObjectId is { } minted)
            {
                sqlObjectByStatement[claim.Evidence] = minted;
            }

            relations.Add(RelationClaim(
                claim.OwnerId,
                sqlObjectId?.ToFactId(),
                AccessKind(claim.Operation),
                claim.ShapeConfidence,
                sqlObjectId is null ? Reason(claim) : null,
                AccessDetails(claim),
                claim.Evidence));
        }

        return sqlObjectByStatement;
    }

    private static ImmutableArray<RelationDetail> AccessDetails(RawDatabaseClaim claim)
    {
        var details = ImmutableArray.CreateBuilder<RelationDetail>();
        details.Add(new RelationDetail(OperationKey, DatabaseFactWire.Name(claim.Operation)));
        if (claim.ObjectText is { } objectText)
        {
            details.Add(new RelationDetail(TargetTextKey, objectText));
        }

        // DAD-28: the statement survives as evidence a human can resolve. It is null whenever the
        // capture guard withheld it, which is a missing detail rather than an error.
        if (claim.SqlText is { } sqlText)
        {
            details.Add(new RelationDetail(SqlKey, sqlText));
        }

        return details.ToImmutable();
    }

    /// <summary>
    /// DAD-08, DAD-09, DAD-11, DAD-12 and DAD-24..DAD-26: every column claim becomes a relation whose
    /// coarse kind carries the direction and whose <c>usage</c> detail carries the precise use. A
    /// tracked write names no entity, so pass two attributes it against the entities the run's contexts
    /// expose - one match is heuristic, several are a candidate, none is silence.
    /// </summary>
    private static void EmitColumnAccesses(
        DatabaseClaimSnapshot snapshot,
        ISymbolIndex symbols,
        Dictionary<(string Entity, string Property), DatabaseColumnFactId> columnByProperty,
        Dictionary<Evidence, DatabaseObjectFactId> sqlObjectByStatement,
        ColumnCatalogue columns,
        ImmutableArray<RawRelation>.Builder relations)
    {
        var exposedEntities = snapshot.Claims
            .Where(static claim => claim.Kind is DatabaseClaimKind.EntitySetExposed)
            .Select(static claim => claim.EntityText)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();

        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.ColumnAccess)
            {
                continue;
            }

            if (claim is { EntityText: { } entityName, PropertyText: { } propertyName })
            {
                // DAD-08 and DAD-09: the reference is proven, but its column is only as resolved as the
                // entity's own mapping - a convention name never becomes a target.
                var mapped = ColumnOf(entityName, propertyName);
                EmitColumnAccess(
                    claim,
                    mapped,
                    propertyName,
                    mapped is null ? FactResolution.Heuristic : FactResolution.Exact,
                    mapped is null ? ConventionMappingReason : null,
                    relations);
                continue;
            }

            if (claim is { PropertyText: { } assignedProperty, ColumnText: { } observedText })
            {
                EmitTrackedWrite(claim, assignedProperty, observedText);
                continue;
            }

            if (claim.ColumnText is not { } columnName)
            {
                continue;
            }

            // DAD-24..DAD-26: a SQL column is proven by the same literal its statement was, so it mints
            // a node whenever that statement resolved to an object to own it.
            var columnId = sqlObjectByStatement.TryGetValue(claim.Evidence, out var owner)
                ? columns.Mint(owner, columnName, FactResolution.Exact, claim)
                : (DatabaseColumnFactId?)null;
            EmitColumnAccess(
                claim,
                columnId,
                columnName,
                claim.ShapeConfidence,
                columnId is null ? Reason(claim) : null,
                relations);
        }

        DatabaseColumnFactId? ColumnOf(string entityName, string propertyName) =>
            columnByProperty.TryGetValue((entityName, propertyName), out var columnId) ? columnId : null;

        void EmitTrackedWrite(RawDatabaseClaim claim, string propertyName, string observedText)
        {
            var matches = exposedEntities
                .Where(entity => symbols.FindMembers(entity, propertyName)
                    .Any(static symbol => symbol.SymbolKind == PropertyDeclarationKind))
                .ToArray();
            if (matches is [])
            {
                return;
            }

            // DAD-11: one match attributes the write heuristically - the entity is inferred from a name
            // match, never proven. DAD-12: several matches stay a candidate rather than a coin flip.
            var columnId = matches is [var single] ? ColumnOf(single, propertyName) : null;
            relations.Add(RelationClaim(
                claim.OwnerId,
                columnId?.ToFactId(),
                ColumnKind(claim.Usage),
                matches is [_] ? FactResolution.Heuristic : FactResolution.Candidate,
                columnId is null
                    ? matches is [_] ? ConventionMappingReason : AmbiguousEntityReason
                    : null,
                [
                    new RelationDetail(UsageKey, DatabaseFactWire.Name(claim.Usage)),
                    new RelationDetail(TargetTextKey, observedText),
                ],
                claim.Evidence));
        }
    }

    private static void EmitColumnAccess(
        RawDatabaseClaim claim,
        DatabaseColumnFactId? columnId,
        string observedText,
        FactResolution resolution,
        string? unresolvedReason,
        ImmutableArray<RawRelation>.Builder relations) =>
        relations.Add(RelationClaim(
            claim.OwnerId,
            columnId?.ToFactId(),
            ColumnKind(claim.Usage),
            resolution,
            unresolvedReason,
            [
                new RelationDetail(UsageKey, DatabaseFactWire.Name(claim.Usage)),
                new RelationDetail(TargetTextKey, observedText),
            ],
            claim.Evidence));

    /// <summary>DAD-14: the claim's own reason, or the resolver's floor when it carried none.</summary>
    private static string Reason(RawDatabaseClaim claim) =>
        string.IsNullOrWhiteSpace(claim.UnresolvedReason) ? UnresolvedTargetReason : claim.UnresolvedReason;

    /// <summary>
    /// The coarse direction an operation reads as. The relation kind stays a short, closed set so
    /// "which tables does this service write?" is one predicate; the precise verb rides as a detail.
    /// </summary>
    private static string AccessKind(DatabaseOperation operation) => operation switch
    {
        DatabaseOperation.Read => ReadsKind,
        DatabaseOperation.Insert or DatabaseOperation.Update or DatabaseOperation.Delete => WritesKind,
        DatabaseOperation.Execute => ExecutesKind,
        _ => AccessesKind,
    };

    /// <summary>
    /// The coarse direction a column usage reads as. P1's analyzers emit only read, write and filter;
    /// the P2 usages (join, order, group, aggregate) are all read-shaped, and the precise value always
    /// rides as the <c>usage</c> detail.
    /// </summary>
    private static string ColumnKind(ColumnUsage usage) => usage switch
    {
        ColumnUsage.Write => WritesColumnKind,
        ColumnUsage.Filter => FiltersByKind,
        _ => ReadsColumnKind,
    };

    /// <summary>
    /// The single indexed type this entity name denotes, or <c>null</c> when the run holds none or
    /// several. A mapping sourced at a guessed symbol would be a fabricated edge, and a fragment
    /// referencing an absent symbol fails <c>C2M-FV-002</c>, so no relation is emitted either way.
    /// </summary>
    private static FactId? EntitySymbol(ISymbolIndex symbols, string entityName)
    {
        var candidates = symbols.FindByName(entityName)
            .Where(static symbol => EntityDeclarationKinds.Contains(symbol.SymbolKind))
            .ToArray();
        return candidates is [var single] ? single.SymbolId.ToFactId() : null;
    }

    /// <summary>
    /// The nodes minted so far, keyed by identity. Two documents naming the same object converge on one
    /// entry whose evidence is the union of theirs, which is the same reconciliation DAD-19 performs
    /// across fragments.
    /// </summary>
    private sealed class ObjectCatalogue
    {
        private readonly Dictionary<DatabaseObjectFactId, ResolvedDatabaseObject> _byId = [];

        public DatabaseObjectFactId Mint(
            DatabaseObjectKind kind,
            string name,
            FactResolution resolution,
            RawDatabaseClaim claim)
        {
            var objectId = DatabaseObjectFactId.Create(DatabaseObjectFactId.UnknownConnection, kind, name);
            _byId[objectId] = _byId.TryGetValue(objectId, out var existing)
                ? existing with
                {
                    Resolution = FactResolutionAlgebra.Stronger(existing.Resolution, resolution),
                    Evidence = Merge(existing.Evidence, claim.Evidence),
                    AnalyzerIds = Merge(existing.AnalyzerIds, claim.AnalyzerId),
                }
                : new ResolvedDatabaseObject(
                    objectId, kind, name, resolution, [claim.Evidence], [claim.AnalyzerId]);
            return objectId;
        }

        public ImmutableArray<ResolvedDatabaseObject> ToImmutable() =>
            _byId.Values
                .OrderBy(static entry => entry.ObjectId.Value, StringComparer.Ordinal)
                .ToImmutableArray();
    }

    /// <summary>The column nodes minted so far, reconciled by identity exactly as objects are.</summary>
    private sealed class ColumnCatalogue
    {
        private readonly Dictionary<DatabaseColumnFactId, ResolvedDatabaseColumn> _byId = [];

        public DatabaseColumnFactId Mint(
            DatabaseObjectFactId objectId,
            string name,
            FactResolution resolution,
            RawDatabaseClaim claim)
        {
            var columnId = DatabaseColumnFactId.Create(objectId, name);
            _byId[columnId] = _byId.TryGetValue(columnId, out var existing)
                ? existing with
                {
                    Resolution = FactResolutionAlgebra.Stronger(existing.Resolution, resolution),
                    Evidence = Merge(existing.Evidence, claim.Evidence),
                    AnalyzerIds = Merge(existing.AnalyzerIds, claim.AnalyzerId),
                }
                : new ResolvedDatabaseColumn(
                    columnId, objectId, name, resolution, [claim.Evidence], [claim.AnalyzerId]);
            return columnId;
        }

        public ImmutableArray<ResolvedDatabaseColumn> ToImmutable() =>
            _byId.Values
                .OrderBy(static entry => entry.ColumnId.Value, StringComparer.Ordinal)
                .ToImmutableArray();
    }

    private static ImmutableArray<Evidence> Merge(ImmutableArray<Evidence> evidence, Evidence addition) =>
        evidence.Contains(addition) ? evidence : [.. evidence.Append(addition).Order()];

    private static ImmutableArray<DataAccessAnalyzerId> Merge(
        ImmutableArray<DataAccessAnalyzerId> analyzerIds,
        DataAccessAnalyzerId addition) =>
        analyzerIds.Contains(addition)
            ? analyzerIds
            : [.. analyzerIds.Append(addition).OrderBy(static id => id.Value, StringComparer.Ordinal)];
}
