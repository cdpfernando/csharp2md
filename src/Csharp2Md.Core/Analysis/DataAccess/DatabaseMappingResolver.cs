using System.Collections.Frozen;
using Csharp2Md.Core.Analysis.Indexes;
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
/// One persistence relation the resolver decided on: its source, the target it could prove (or
/// <c>null</c> plus a reason when it could not), and the details design.md's relation table names.
/// </summary>
internal sealed record ResolvedDatabaseRelation(
    FactId SourceId,
    FactId? TargetId,
    string RelationKind,
    FactResolution Resolution,
    string? UnresolvedReason,
    ImmutableArray<RelationDetail> Details,
    Evidence Evidence,
    DataAccessAnalyzerId AnalyzerId);

/// <summary>Everything pass two decided, ready to become one solution-level fragment.</summary>
internal sealed record DatabaseResolution(
    ImmutableArray<ResolvedDatabaseObject> Objects,
    ImmutableArray<ResolvedDatabaseColumn> Columns,
    ImmutableArray<ResolvedDatabaseRelation> Relations,
    ImmutableArray<DocumentExtent> Documents)
{
    public static DatabaseResolution Empty { get; } = new([], [], [], []);

    public bool IsEmpty => Objects.IsEmpty && Columns.IsEmpty && Relations.IsEmpty;
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

    internal const string TargetTextKey = "target_text";
    internal const string MappingKey = "mapping";

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
        var relations = ImmutableArray.CreateBuilder<ResolvedDatabaseRelation>();

        // Every configured table is read before any mapping is emitted, which is what makes DAD-04's
        // precedence independent of the order documents were analysed in.
        var objectByEntity = ResolveEntityTables(snapshot, objects);
        var columnByProperty = ResolveEntityColumns(snapshot, objectByEntity, columns);
        EmitEntitySetExposures(snapshot, relations);
        var mappedEntities = EmitEntityMappings(snapshot, symbols, objectByEntity, relations);
        EmitPropertyMappings(snapshot, symbols, mappedEntities, columnByProperty, relations);

        return new DatabaseResolution(
            objects.ToImmutable(), columns.ToImmutable(), relations.ToImmutable(), snapshot.Documents);
    }

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
        ImmutableArray<ResolvedDatabaseRelation>.Builder relations)
    {
        foreach (var claim in snapshot.Claims)
        {
            if (claim.Kind is not DatabaseClaimKind.EntitySetExposed || claim.EntityText is not { } entityName)
            {
                continue;
            }

            relations.Add(new ResolvedDatabaseRelation(
                claim.OwnerId,
                null,
                ExposesKind,
                claim.ShapeConfidence,
                EntitySetTargetReason,
                [new RelationDetail(TargetTextKey, entityName)],
                claim.Evidence,
                claim.AnalyzerId));
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
        ImmutableArray<ResolvedDatabaseRelation>.Builder relations)
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

            relations.Add(new ResolvedDatabaseRelation(
                source,
                objectId.ToFactId(),
                MapsToKind,
                FactResolution.Exact,
                null,
                [new RelationDetail(TargetTextKey, tableName), new RelationDetail(MappingKey, ConfiguredMapping)],
                claim.Evidence,
                claim.AnalyzerId));
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

            relations.Add(new ResolvedDatabaseRelation(
                source,
                null,
                MapsToKind,
                FactResolution.Heuristic,
                ConventionMappingReason,
                [new RelationDetail(TargetTextKey, setName), new RelationDetail(MappingKey, ConventionMapping)],
                claim.Evidence,
                claim.AnalyzerId));
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
        ImmutableArray<ResolvedDatabaseRelation>.Builder relations)
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
            relations.Add(new ResolvedDatabaseRelation(
                source,
                columnId,
                MapsPropertyToColumnKind,
                columnId is null ? FactResolution.Unresolved : FactResolution.Exact,
                columnId is null ? UnmappedOwningObjectReason : null,
                [new RelationDetail(TargetTextKey, columnName), new RelationDetail(MappingKey, ConfiguredMapping)],
                claim.Evidence,
                claim.AnalyzerId));
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

                relations.Add(new ResolvedDatabaseRelation(
                    property.SymbolId.ToFactId(),
                    null,
                    MapsPropertyToColumnKind,
                    FactResolution.Heuristic,
                    ConventionMappingReason,
                    [
                        new RelationDetail(TargetTextKey, property.Name),
                        new RelationDetail(MappingKey, ConventionMapping),
                    ],
                    anchor.Evidence,
                    anchor.AnalyzerId));
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
