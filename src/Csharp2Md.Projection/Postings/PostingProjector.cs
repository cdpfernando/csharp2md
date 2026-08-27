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

    public static ImmutableArray<StagedFragment> Project(PublishedPackageView view)
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
        Add(fragments, OutgoingKey, outgoing);
        Add(fragments, IncomingKey, incoming);
        Add(fragments, CallersKey, callers);
        Add(fragments, CalleesKey, callees);
        Add(fragments, ContractProducersKey, producers);
        Add(fragments, ContractConsumersKey, consumers);
        Add(fragments, DataReadersKey, readers);
        Add(fragments, DataWritersKey, writers);
        return fragments.ToImmutable();
    }

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
        Dictionary<string, List<PostingEntryDto>> groups)
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
            .ToImmutableArray();
        fragments.Add(new StagedFragment(ArtifactRole.Payload, key, CanonicalJson.Write(payload)));
    }
}
