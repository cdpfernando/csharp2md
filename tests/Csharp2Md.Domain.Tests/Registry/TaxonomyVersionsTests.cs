using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class TaxonomyVersionsTests
{
    [Fact]
    [Trait("Requirement", "TAX-83")]
    public void TaxonomyVersions_Initial_HasAllFiveAxesStartingAtOne()
    {
        var versions = TaxonomyVersions.Initial;

        Assert.Equal(1, versions.SchemaVersion);
        Assert.Equal(1, versions.TaxonomyVersion);
        Assert.Equal(1, versions.ObservationSchemaVersion);
        Assert.Equal(1, versions.ExtractorSetVersion);
        Assert.Equal(1, versions.ClassifierSetVersion);
    }

    [Fact]
    [Trait("Requirement", "TAX-83")]
    public void TaxonomyVersions_HasExactlyFiveIndependentIntegerAxisMembers()
    {
        var integerProperties = typeof(TaxonomyVersions).GetProperties().Where(property => property.PropertyType == typeof(int)).ToArray();

        Assert.Equal(5, integerProperties.Length);
        Assert.Equal(
            new[] { "SchemaVersion", "TaxonomyVersion", "ObservationSchemaVersion", "ExtractorSetVersion", "ClassifierSetVersion" }.ToHashSet(),
            integerProperties.Select(property => property.Name).ToHashSet());
    }

    [Fact]
    [Trait("Requirement", "TAX-83")]
    public void TaxonomyVersions_ChangingOneAxisLeavesTheOtherFourUnaffected()
    {
        var bumped = TaxonomyVersions.Initial with { TaxonomyVersion = 2 };

        Assert.Equal(2, bumped.TaxonomyVersion);
        Assert.Equal(1, bumped.SchemaVersion);
        Assert.Equal(1, bumped.ObservationSchemaVersion);
        Assert.Equal(1, bumped.ExtractorSetVersion);
        Assert.Equal(1, bumped.ClassifierSetVersion);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void TaxonomyTables_Default_EnumeratesEveryDeclaredTableFromOnePlace()
    {
        var tables = TaxonomyTables.Default;

        Assert.Equal(FactTypeTable.All, tables.FactTypes);
        Assert.Equal(RelationTable.All, tables.Relations);
        Assert.Equal(ObservationKindTable.All, tables.ObservationKinds);
        Assert.Equal(MappingRoleTable.All, tables.MappingRoles);
        Assert.Equal(PayloadRoleTable.All, tables.PayloadRoles);
        Assert.Equal(FacetAxisTable.All, tables.FacetAxes);
        Assert.Equal(ProofAxisTable.All, tables.ProofAxes);

        // GCPC-019/F3 (spec.md P1: Registry and version axes, user-confirmed): this feature interned
        // the wire encoding, changed the manifest shape, added envelopes, changed what is extracted and
        // changed how it is classified -- schema_version, taxonomy_version, extractor_set_version and
        // classifier_set_version each advance to 2; observation_schema_version is untouched.
        Assert.Equal(
            TaxonomyVersions.Initial with
            {
                SchemaVersion = 2,
                TaxonomyVersion = 2,
                ExtractorSetVersion = 2,
                ClassifierSetVersion = 2,
            },
            tables.Versions);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void TaxonomyTables_Default_MovesSchemaTaxonomyExtractorAndClassifierVersionsToTwo()
    {
        var versions = TaxonomyTables.Default.Versions;

        Assert.Equal(2, versions.SchemaVersion);
        Assert.Equal(2, versions.TaxonomyVersion);
        Assert.Equal(2, versions.ExtractorSetVersion);
        Assert.Equal(2, versions.ClassifierSetVersion);
        Assert.Equal(TaxonomyVersions.Initial.ObservationSchemaVersion, versions.ObservationSchemaVersion);
    }
}
