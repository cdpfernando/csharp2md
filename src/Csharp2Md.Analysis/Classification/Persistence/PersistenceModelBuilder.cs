using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;

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
        return
        [
            .. contextTypes.Select(contextType => new StoreNode(
                contextType,
                DataStoreTechnology.Relational,
                ResolveStoreName(context, contextType, references),
                [])),
        ];
    }

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
