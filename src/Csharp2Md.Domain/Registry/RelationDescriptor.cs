using System.Collections.Frozen;
using Csharp2Md.Domain.Proof;
using Csharp2Md.Domain.Relations;

namespace Csharp2Md.Domain.Registry;

public readonly record struct RelationTriple(string SourceFactType, string TargetFactType);

public sealed record RelationDescriptor(
    RelationKind Kind,
    string WireName,
    ImmutableArray<RelationTriple> Triples,
    EvidenceMethod MinimumEvidenceMethod);

internal readonly record struct RelationTripleKey(RelationKind Kind, string SourceFactType, string TargetFactType);

internal static class RelationTripleIndex
{
    public static FrozenSet<RelationTripleKey> Build(ImmutableArray<RelationDescriptor> relations)
    {
        var keys = new HashSet<RelationTripleKey>();
        foreach (var relation in relations)
        {
            foreach (var triple in relation.Triples)
            {
                var key = new RelationTripleKey(relation.Kind, triple.SourceFactType, triple.TargetFactType);
                if (!keys.Add(key))
                {
                    throw new ArgumentException(
                        $"The triple '{triple.SourceFactType}' -[{relation.Kind}]-> '{triple.TargetFactType}' is declared twice in the relation matrix.",
                        nameof(relations));
                }
            }
        }

        return keys.ToFrozenSet();
    }
}

internal static class RelationTable
{
    public static readonly ImmutableArray<RelationDescriptor> All =
    [
        new(
            RelationKind.Contains,
            "contains",
            [
                new("Solution", "Project"),
                new("Project", "Document"),
                new("Document", "Symbol"),
            ],
            EvidenceMethod.Syntactic),
        new(
            RelationKind.BelongsTo,
            "belongs-to",
            [new("Symbol", "Component")],
            EvidenceMethod.Semantic),
        new(
            RelationKind.IncludedIn,
            "included-in",
            [new("Component", "DeploymentUnit")],
            EvidenceMethod.Configured),
        new(
            RelationKind.Executes,
            "executes",
            [new("EntryPoint", "Symbol")],
            EvidenceMethod.Semantic),
        new(
            RelationKind.Invokes,
            "invokes",
            [new("Symbol", "Symbol")],
            EvidenceMethod.Semantic),
        new(
            RelationKind.ImplementsOperation,
            "implements-operation",
            [new("Symbol", "BoundaryOperation")],
            EvidenceMethod.Semantic),
        new(
            RelationKind.Targets,
            "targets",
            [
                new("BoundaryOperation", "BoundaryOperation"),
                new("BoundaryOperation", "DeploymentUnit"),
                new("BoundaryOperation", "ExternalSystem"),
            ],
            EvidenceMethod.Configured),
        new(
            RelationKind.UsesContract,
            "uses-contract",
            [new("BoundaryOperation", "Contract")],
            EvidenceMethod.Semantic),
        new(
            RelationKind.AccessesData,
            "accesses-data",
            [new("Symbol", "DataOperation")],
            EvidenceMethod.Semantic),
        new(
            RelationKind.OperatesOn,
            "operates-on",
            [
                new("DataOperation", "DataObject"),
                new("DataOperation", "DataField"),
            ],
            EvidenceMethod.Semantic),
        new(
            RelationKind.MapsTo,
            "maps-to",
            [
                new("Symbol", "Contract"),
                new("Symbol", "DataObject"),
                new("Symbol", "DataField"),
            ],
            EvidenceMethod.Configured),
        new(
            RelationKind.ConfiguredBy,
            "configured-by",
            [
                new("Symbol", "ConfigurationBinding"),
                new("BoundaryOperation", "ConfigurationBinding"),
                new("DataStore", "ConfigurationBinding"),
                new("Component", "ConfigurationBinding"),
            ],
            EvidenceMethod.Configured),
    ];

    public static readonly FrozenSet<RelationTripleKey> TripleLookup = RelationTripleIndex.Build(All);
}

public sealed partial record TaxonomyTables
{
    public ImmutableArray<RelationDescriptor> Relations { get; init; } = [];
}
