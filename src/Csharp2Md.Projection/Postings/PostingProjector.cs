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

    private static readonly string InvokesWireName =
        TaxonomyTables.Default.Relations.Single(static descriptor => descriptor.Kind == RelationKind.Invokes).WireName;

    public static ImmutableArray<StagedFragment> Project(PublishedPackageView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var outgoing = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var incoming = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var callers = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        var callees = new Dictionary<string, List<PostingEntryDto>>(StringComparer.Ordinal);
        foreach (var descriptor in TaxonomyTables.Default.Relations)
        {
            if (!view.Document.ConfirmedRelations.TryGetValue(descriptor.WireName, out var records)
                || records.IsDefaultOrEmpty)
            {
                continue;
            }

            var isInvokes = string.Equals(descriptor.WireName, InvokesWireName, StringComparison.Ordinal);
            for (var index = 0; index < records.Length; index++)
            {
                if (!view.TryLocateRelation(descriptor.WireName, index, out var citation))
                {
                    continue;
                }

                var entry = new PostingEntryDto(citation.ArtifactKey, citation.Ordinal);
                Add(outgoing, records[index].Source.Id, entry);
                Add(incoming, records[index].Target.Id, entry);
                if (isInvokes)
                {
                    Add(callers, records[index].Target.Id, entry);
                    Add(callees, records[index].Source.Id, entry);
                }
            }
        }

        var fragments = ImmutableArray.CreateBuilder<StagedFragment>();
        Add(fragments, OutgoingKey, outgoing);
        Add(fragments, IncomingKey, incoming);
        Add(fragments, CallersKey, callers);
        Add(fragments, CalleesKey, callees);
        return fragments.ToImmutable();
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
