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
        // F3: this feature interned the wire encoding, changed the manifest shape, added envelopes,
        // changed what is extracted and changed how it is classified -- schema_version,
        // extractor_set_version and classifier_set_version advance to 2 alongside taxonomy_version.
        // observation_schema_version is untouched by this feature and stays at 1.
        Versions = TaxonomyVersions.Initial with
        {
            SchemaVersion = 2,
            TaxonomyVersion = 2,
            ExtractorSetVersion = 2,
            ClassifierSetVersion = 2,
        },
    };
}
