using System.Collections.Frozen;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Registry;

public sealed class TaxonomyRegistry
{
    private readonly FrozenSet<RelationTripleKey> _tripleLookup;

    public TaxonomyTables Tables { get; }

    public TaxonomyRegistry(TaxonomyTables tables)
    {
        ArgumentNullException.ThrowIfNull(tables);

        Tables = tables;
        _tripleLookup = RelationTripleIndex.Build(tables.Relations);
    }

    public bool IsRegisteredTriple(RelationKind kind, string sourceFactType, string targetFactType) =>
        _tripleLookup.Contains(new RelationTripleKey(kind, sourceFactType, targetFactType));

    public void RequireRegisteredTriple(RelationKind kind, string sourceFactType, string targetFactType)
    {
        if (!IsRegisteredTriple(kind, sourceFactType, targetFactType))
        {
            throw new ArgumentException(
                $"The triple '{sourceFactType}' -[{kind}]-> '{targetFactType}' is not registered in the taxonomy.");
        }
    }

    public EvidenceMethod MinimumEvidenceMethod(RelationKind kind)
    {
        foreach (var relation in Tables.Relations)
        {
            if (relation.Kind == kind)
            {
                return relation.MinimumEvidenceMethod;
            }
        }

        throw new ArgumentException($"'{kind}' has no registered relation descriptor.", nameof(kind));
    }

    public void RequireSufficientEvidence(RelationKind kind, EvidenceMethod supplied)
    {
        var required = MinimumEvidenceMethod(kind);
        if (supplied != required)
        {
            throw new ArgumentException(
                $"Relation '{kind}' requires evidence method '{required}' but '{supplied}' was supplied.");
        }
    }
}
