using System.Text.Json.Nodes;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Registry;
using Csharp2Md.Domain.Relations;
using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Postings;

internal static class PostingProjector
{
    internal const string OutgoingKey = "postings/outgoing.json";
    internal const string IncomingKey = "postings/incoming.json";
    internal const string CallersKey = "postings/callers.json";
    internal const string CalleesKey = "postings/callees.json";
    internal const string ContractProducersKey = "postings/contract-producers.json";
    internal const string ContractConsumersKey = "postings/contract-consumers.json";
    internal const string DataReadersKey = "postings/data-readers.json";
    internal const string DataWritersKey = "postings/data-writers.json";
    internal const string UnknownsKey = "postings/unknowns.json";
    internal const string FrontiersKey = "postings/frontiers.json";

    public static ImmutableArray<StagedFragment> Project(
        PublishedPackageView view,
        int ceilingBytes = ShardWriter.DefaultCeilingBytes)
    {
        ArgumentNullException.ThrowIfNull(view);

        var outgoing = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var incoming = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var callers = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var callees = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var producers = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var consumers = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var readers = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var writers = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        foreach (var descriptor in TaxonomyTables.Default.Relations)
        {
            if (!view.Document.ConfirmedRelations.TryGetValue(descriptor.WireName, out var records)
                || records.IsDefaultOrEmpty)
            {
                continue;
            }

            for (var index = 0; index < records.Length; index++)
            {
                if (!view.TryLocateRelation(descriptor.WireName, index, out var citation))
                {
                    continue;
                }

                var relation = records[index];
                var entry = new PostingEntryDto(citation.ArtifactKey, citation.Ordinal);
                Add(outgoing, relation.Source.Id, entry);
                Add(incoming, relation.Target.Id, entry);
                switch (descriptor.Kind)
                {
                    case RelationKind.Invokes:
                        Add(callers, relation.Target.Id, entry);
                        Add(callees, relation.Source.Id, entry);
                        break;
                    case RelationKind.UsesContract:
                        AddContractRole(view, producers, consumers, relation, entry);
                        break;
                    case RelationKind.OperatesOn:
                        AddDataRole(view, readers, writers, relation, entry);
                        break;
                }
            }
        }

        var fragments = ImmutableArray.CreateBuilder<StagedFragment>();
        Add(fragments, OutgoingKey, outgoing, ceilingBytes);
        Add(fragments, IncomingKey, incoming, ceilingBytes);
        Add(fragments, CallersKey, callers, ceilingBytes);
        Add(fragments, CalleesKey, callees, ceilingBytes);
        Add(fragments, ContractProducersKey, producers, ceilingBytes);
        Add(fragments, ContractConsumersKey, consumers, ceilingBytes);
        Add(fragments, DataReadersKey, readers, ceilingBytes);
        Add(fragments, DataWritersKey, writers, ceilingBytes);
        AddIndexed(
            fragments,
            UnknownsKey,
            view.Document.Unresolved.Select(static (record, ordinal) => (record.Source.Id, ordinal)),
            view.TryLocateUnresolved,
            ceilingBytes);
        AddIndexed(
            fragments,
            FrontiersKey,
            view.Document.Frontiers.Select(static (frontier, ordinal) => (frontier.Occurrence.Owner.Id, ordinal)),
            view.TryLocateFrontier,
            ceilingBytes);
        return fragments.ToImmutable();
    }

    /// <summary>
    /// GCPC-091: a contract's producer publishes it, its consumer handles it. For a messaging binding
    /// -- the only kind <c>ContractPass</c> creates today -- that distinction is
    /// <see cref="BoundaryOperationDto.Direction"/> (outbound publishes, inbound handles), never
    /// <see cref="ContractBindingDto.PayloadRole"/>: <c>ContractPass</c> assigns every messaging
    /// binding, producer and consumer alike, the same <c>"request"</c> payload role (EBC-22/EBC-26;
    /// there is no messaging "response"), so a role-only split put every messaging operation in
    /// <c>consumers</c> and left <c>producers</c> permanently empty. This is additive, scoped to
    /// <c>Protocol == "messaging"</c> only: a non-messaging (HTTP-shaped) binding keeps using
    /// <see cref="ContractBindingDto.PayloadRole"/> exactly as before (RP-25 -- role decides, not the
    /// operation's own name or direction), so that existing, tested behavior does not change.
    /// </summary>
    private static void AddContractRole(
        PublishedPackageView view,
        Dictionary<string, List<PostingEntryDto>> producers,
        Dictionary<string, List<PostingEntryDto>> consumers,
        ConfirmedRelationDto relation,
        PostingEntryDto entry)
    {
        var binding = view.Document.ContractBindings.FirstOrDefault(candidate =>
            string.Equals(candidate.Operation.Id, relation.Source.Id, StringComparison.Ordinal)
            && string.Equals(candidate.Contract.Id, relation.Target.Id, StringComparison.Ordinal));
        if (binding is null)
        {
            return;
        }

        var operation = view.Document.BoundaryOperations.FirstOrDefault(candidate =>
            string.Equals(candidate.Identity.Id, binding.Operation.Id, StringComparison.Ordinal));
        if (operation is not null && string.Equals(operation.Protocol, "messaging", StringComparison.Ordinal))
        {
            if (string.Equals(operation.Direction, "outbound", StringComparison.Ordinal))
            {
                Add(producers, relation.Target.Id, entry);
            }
            else if (string.Equals(operation.Direction, "inbound", StringComparison.Ordinal))
            {
                Add(consumers, relation.Target.Id, entry);
            }

            return;
        }

        if (string.Equals(binding.PayloadRole, "response", StringComparison.Ordinal))
        {
            Add(producers, relation.Target.Id, entry);
            return;
        }

        if (binding.PayloadRole is "request" or "header" or "query-parameter")
        {
            Add(consumers, relation.Target.Id, entry);
        }
    }

    private static void AddDataRole(
        PublishedPackageView view,
        Dictionary<string, List<PostingEntryDto>> readers,
        Dictionary<string, List<PostingEntryDto>> writers,
        ConfirmedRelationDto relation,
        PostingEntryDto entry)
    {
        var operation = view.Document.DataOperations.FirstOrDefault(candidate =>
            string.Equals(candidate.Identity.Id, relation.Source.Id, StringComparison.Ordinal));
        if (operation is null)
        {
            return;
        }

        if (string.Equals(operation.Operation, "read", StringComparison.Ordinal))
        {
            Add(readers, relation.Target.Id, entry);
            return;
        }

        if (operation.Operation is "insert" or "update" or "delete" or "execute")
        {
            Add(writers, relation.Target.Id, entry);
        }
    }

    private static void Add(
        Dictionary<string, List<PostingEntryDto>> groups,
        string factId,
        PostingEntryDto entry)
    {
        if (!groups.TryGetValue(factId, out var entries))
        {
            entries = [];
            groups[factId] = entries;
        }

        entries.Add(entry);
    }

    private static void Add(
        ImmutableArray<StagedFragment>.Builder fragments,
        string key,
        Dictionary<string, List<PostingEntryDto>> groups,
        int ceilingBytes)
    {
        if (groups.Count == 0)
        {
            return;
        }

        var payload = groups
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => new PostingGroupDto(
                pair.Key,
                [
                    .. pair.Value
                        .OrderBy(static entry => entry.ArtifactKey, StringComparer.Ordinal)
                        .ThenBy(static entry => entry.Ordinal),
                ]))
            .ToArray();
        fragments.AddRange(ShardWriter.Write(key, Nodes(payload), ceilingBytes));
    }

    /// <summary>Resolves the citation for the record at <paramref name="index"/> in a family's own
    /// document-order array (<see cref="PublishedPackageView.TryLocateUnresolved"/> and
    /// <see cref="PublishedPackageView.TryLocateFrontier"/>), matching <c>out</c>-parameter delegates
    /// <see cref="Func{T,TResult}"/> cannot express.</summary>
    private delegate bool TryLocateIndexed(int index, out ArtifactCitation citation);

    /// <summary>
    /// GCPC-040/GCPC-041: <paramref name="items"/>'s ordinal is the record's position in the document's own
    /// (unsplit) array, not necessarily its ordinal inside whatever artifact the family split into --
    /// <paramref name="locate"/> resolves the real, shard-aware citation instead of assuming the family
    /// stayed in a single base-key artifact.
    /// </summary>
    private static void AddIndexed(
        ImmutableArray<StagedFragment>.Builder fragments,
        string postingKey,
        IEnumerable<(string FactId, int Ordinal)> items,
        TryLocateIndexed locate,
        int ceilingBytes)
    {
        var groups = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        foreach (var (factId, ordinal) in items)
        {
            if (!locate(ordinal, out var citation))
            {
                continue;
            }

            Add(groups, factId, new PostingEntryDto(citation.ArtifactKey, citation.Ordinal));
        }

        Add(fragments, postingKey, groups, ceilingBytes);
    }

    private static List<(string FactId, JsonNode Entry)> Nodes(IReadOnlyList<PostingGroupDto> groups)
    {
        var nodes = new List<(string FactId, JsonNode Entry)>(groups.Count);
        foreach (var group in groups)
        {
            nodes.Add((
                group.FactId,
                JsonNode.Parse(CanonicalJson.Write(group).AsSpan())
                ?? throw new InvalidOperationException("Canonical posting group parsed to null.")));
        }

        return nodes;
    }
}
