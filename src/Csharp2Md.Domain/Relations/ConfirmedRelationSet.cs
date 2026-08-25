using System.Collections;

namespace Csharp2Md.Domain.Relations;

public sealed class ConfirmedRelationSet : IReadOnlyCollection<ConfirmedRelation>
{
    private readonly List<ConfirmedRelation> _ordered = [];
    private readonly HashSet<ConfirmedRelation> _seen = [];

    public int Count => _ordered.Count;

    public void Add(ConfirmedRelation relation)
    {
        ArgumentNullException.ThrowIfNull(relation);

        if (_seen.Add(relation))
        {
            _ordered.Add(relation);
        }
    }

    public IEnumerator<ConfirmedRelation> GetEnumerator() => _ordered.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
