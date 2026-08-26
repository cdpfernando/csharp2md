using Csharp2Md.Analysis.Classification;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Facets;
using Csharp2Md.Domain.Facts;
using Csharp2Md.Domain.Observations;
using Csharp2Md.Domain.Proof;

namespace Csharp2Md.Analysis.Classification.Configuration;

/// <summary>
/// Resolves declared configuration keys and the facts that consume them into a
/// <see cref="ConfigurationModel"/>. The builder reads the ledger and nothing else, so no Roslyn
/// type reaches classification (CDC-08, CDC-35).
/// </summary>
internal static class ConfigurationModelBuilder
{
    internal const string KeyPayloadKey = "key";
    internal const string ResolutionPayloadKey = "resolution";
    internal const string AddressPayloadKey = "address";
    internal const string ConnectionStringsPrefix = "ConnectionStrings:";
    internal const string ServicesPrefix = "Services:";
    internal const string UngroupedDocumentCode = "ungrouped-configuration-document";

    /// <summary>The resolved configuration picture for this snapshot.</summary>
    public static ConfigurationModel Build(ClassifierContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var keys = CollectDeclaredKeys(context);
        var (edges, unbound) = CorrelateConsumers(context, keys);
        var bound = edges
            .Select(static edge => edge.KeyPath)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return new ConfigurationModel(
            keys,
            edges,
            [],
            unbound,
            new ConfigurationCoverage(keys.Length, bound, unbound.Length));
    }

    private static ImmutableArray<DeclaredKey> CollectDeclaredKeys(ClassifierContext context)
    {
        var documentsById = context.FactsByType<Document>()
            .ToDictionary(static document => document.Reference.Id.Value, StringComparer.Ordinal);
        var diagnosed = new HashSet<string>(StringComparer.Ordinal);
        var keys = new List<DeclaredKey>();
        foreach (var observation in context.ObservationsByKind(ObservationKind.Configuration)
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal))
        {
            if (!documentsById.TryGetValue(observation.Identity.Owner.Id.Value, out var document))
            {
                continue;
            }

            var keyPath = PayloadReader.Value(observation, KeyPayloadKey);
            if (keyPath is null)
            {
                continue;
            }

            var component = context.ComponentForProject(document.OwningProject);
            if (component is null)
            {
                if (diagnosed.Add(document.Reference.Id.Value))
                {
                    context.Accumulator.AddDiagnostic(
                        new DiagnosticRecord(
                            UngroupedDocumentCode,
                            $"The configuration document '{document.RelativePath}' belongs to a project that is not grouped into a component.",
                            document.RelativePath));
                }

                continue;
            }

            keys.Add(
                new DeclaredKey(
                    keyPath,
                    ParseResolution(PayloadReader.Value(observation, ResolutionPayloadKey)),
                    PayloadReader.Value(observation, AddressPayloadKey),
                    component.Reference,
                    observation.Identity));
        }

        return [.. keys];
    }

    private static (ImmutableArray<ConfiguredEdge> Edges, ImmutableArray<UnboundKeyRead> Unbound) CorrelateConsumers(
        ClassifierContext context,
        ImmutableArray<DeclaredKey> keys)
    {
        var edges = new List<ConfiguredEdge>();
        foreach (var key in keys)
        {
            edges.Add(
                new ConfiguredEdge(
                    key.OwningComponent,
                    key.KeyPath,
                    EvidenceChain.Create([key.Evidence])));
        }

        var documentIds = context.FactsByType<Document>()
            .Select(static document => document.Reference.Id.Value)
            .ToHashSet(StringComparer.Ordinal);
        var unbound = new List<UnboundKeyRead>();
        foreach (var observation in context.ObservationsByKind(ObservationKind.Configuration)
            .OrderBy(static observation => observation.Identity.Owner.Id.Value, StringComparer.Ordinal)
            .ThenBy(static observation => observation.Identity.OccurrenceOrdinal))
        {
            if (documentIds.Contains(observation.Identity.Owner.Id.Value)
                || !string.Equals(observation.Identity.Owner.FactType, nameof(Symbol), StringComparison.Ordinal))
            {
                continue;
            }

            var keyPath = PayloadReader.Value(observation, KeyPayloadKey);
            if (keyPath is null)
            {
                continue;
            }

            var match = MatchKeyPath(keys, keyPath);
            if (match is null)
            {
                unbound.Add(new UnboundKeyRead(observation.Identity.Owner, observation.Identity));
                continue;
            }

            edges.Add(
                new ConfiguredEdge(
                    observation.Identity.Owner,
                    match.KeyPath,
                    EvidenceChain.Create([observation.Identity, match.Evidence])));
        }

        foreach (var store in context.FactsByType<DataStore>()
            .OrderBy(static store => store.Reference.Id.Value, StringComparer.Ordinal))
        {
            var match = MatchLastSegment(keys, ConnectionStringsPrefix, store.Name.Value);
            if (match is null)
            {
                continue;
            }

            edges.Add(
                new ConfiguredEdge(
                    store.Reference,
                    match.KeyPath,
                    EvidenceChain.Create([match.Evidence])));
        }

        foreach (var operation in context.FactsByType<BoundaryOperation>()
            .OrderBy(static operation => operation.Reference.Id.Value, StringComparer.Ordinal))
        {
            if (operation.Direction is not BoundaryDirection.Outbound
                || operation.DestinationScope is null)
            {
                continue;
            }

            var match = MatchLastSegment(keys, ServicesPrefix, operation.DestinationScope);
            if (match is null)
            {
                continue;
            }

            edges.Add(
                new ConfiguredEdge(
                    operation.Reference,
                    match.KeyPath,
                    EvidenceChain.Create([match.Evidence])));
        }

        return (
            [
                .. edges
                    .OrderBy(static edge => edge.Source.Id.Value, StringComparer.Ordinal)
                    .ThenBy(static edge => edge.KeyPath, StringComparer.Ordinal),
            ],
            [
                .. unbound
                    .OrderBy(static read => read.Symbol.Id.Value, StringComparer.Ordinal)
                    .ThenBy(static read => read.Evidence.OccurrenceOrdinal),
            ]);
    }

    private static DeclaredKey? MatchKeyPath(ImmutableArray<DeclaredKey> keys, string keyPath)
    {
        foreach (var key in keys)
        {
            if (string.Equals(key.KeyPath, keyPath, StringComparison.Ordinal))
            {
                return key;
            }
        }

        return null;
    }

    private static DeclaredKey? MatchLastSegment(ImmutableArray<DeclaredKey> keys, string prefix, string name)
    {
        foreach (var key in keys)
        {
            if (key.KeyPath.StartsWith(prefix, StringComparison.Ordinal)
                && string.Equals(LastSegment(key.KeyPath), name, StringComparison.Ordinal))
            {
                return key;
            }
        }

        return null;
    }

    private static string? LastSegment(string keyPath)
    {
        var separator = keyPath.LastIndexOf(':');
        return separator < 0 || separator == keyPath.Length - 1 ? null : keyPath[(separator + 1)..];
    }

    private static KeyResolution ParseResolution(string? value) => value switch
    {
        "literal" => KeyResolution.Literal,
        "dynamic" => KeyResolution.Dynamic,
        "unknown" => KeyResolution.Unknown,
        _ => KeyResolution.Unknown,
    };
}
