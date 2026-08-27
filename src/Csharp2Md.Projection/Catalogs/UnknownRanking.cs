using Csharp2Md.Storage.Mapping;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Projection.Catalogs;

internal readonly record struct RankedUnknown(UnresolvedRecordDto Record, int Ordinal);

internal static class UnknownRanking
{
    public static ImmutableArray<RankedUnknown> Rank(PublishedPackageView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        var unresolved = view.Document.Unresolved;
        if (unresolved.IsDefaultOrEmpty)
        {
            return [];
        }

        var degree = Degrees(view);
        return unresolved
            .Select((record, ordinal) => (Record: record, Ordinal: ordinal, Degree: DegreeOf(degree, record.Source.Id)))
            .OrderByDescending(item => item.Degree)
            .ThenBy(item => item.Record.Source.Id, StringComparer.Ordinal)
            .ThenBy(item => item.Ordinal)
            .Select(item => new RankedUnknown(item.Record, item.Ordinal))
            .ToImmutableArray();
    }

    private static Dictionary<string, int> Degrees(PublishedPackageView view)
    {
        var degree = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var records in view.Document.ConfirmedRelations.Values)
        {
            foreach (var relation in records)
            {
                Add(degree, relation.Source.Id);
                if (!string.Equals(relation.Source.Id, relation.Target.Id, StringComparison.Ordinal))
                {
                    Add(degree, relation.Target.Id);
                }
            }
        }

        return degree;
    }

    private static void Add(Dictionary<string, int> degree, string id) =>
        degree[id] = degree.GetValueOrDefault(id) + 1;

    private static int DegreeOf(Dictionary<string, int> degree, string ownerId) =>
        degree.GetValueOrDefault(ownerId);
}
