namespace Csharp2Md.Domain.Registry;

public sealed record TaxonomyVersions(
    int SchemaVersion,
    int TaxonomyVersion,
    int ObservationSchemaVersion,
    int ExtractorSetVersion,
    int ClassifierSetVersion)
{
    public static TaxonomyVersions Initial { get; } = new(1, 1, 1, 1, 1);
}

public sealed partial record TaxonomyTables
{
    public TaxonomyVersions Versions { get; init; } = TaxonomyVersions.Initial;

    public static TaxonomyTables Default { get; } = new()
    {
        FactTypes = FactTypeTable.All,
        Relations = RelationTable.All,
        MappingRoles = MappingRoleTable.All,
        PayloadRoles = PayloadRoleTable.All,
        ObservationKinds = ObservationKindTable.All,
        FacetAxes = FacetAxisTable.All,
        ProofAxes = ProofAxisTable.All,
        Versions = TaxonomyVersions.Initial with { TaxonomyVersion = 2 },
    };
}
