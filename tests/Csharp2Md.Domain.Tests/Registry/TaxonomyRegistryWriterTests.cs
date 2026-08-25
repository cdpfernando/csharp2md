using System.Text;
using System.Text.Json;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Domain.Tests.Registry;

public sealed class TaxonomyRegistryWriterTests
{
    private static readonly TaxonomyTables Tables = TaxonomyTables.Default;

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_TwoRunsAgainstUnchangedTables_ProduceByteIdenticalOutput()
    {
        var firstBytes = Encoding.UTF8.GetBytes(TaxonomyRegistryWriter.Write(Tables));
        var secondBytes = Encoding.UTF8.GetBytes(TaxonomyRegistryWriter.Write(Tables));

        Assert.Equal(firstBytes, secondBytes);
    }

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_HasNoUtf8Bom()
    {
        var bytes = Encoding.UTF8.GetBytes(TaxonomyRegistryWriter.Write(Tables));
        var expectedBom = new byte[] { 0xEF, 0xBB, 0xBF };

        Assert.NotEqual(expectedBom, bytes.Take(3).ToArray());
    }

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_ContainsNoCarriageReturn()
    {
        var json = TaxonomyRegistryWriter.Write(Tables);

        Assert.DoesNotContain('\r', json);
    }

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_NestedMembersUseExactlyTwoSpaceIndent()
    {
        var lines = TaxonomyRegistryWriter.Write(Tables).Split('\n');

        Assert.Contains(lines, line => line.StartsWith("  \"schema_version\": ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("   \"schema_version\": ", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Requirement", "TAX-85")]
    public void Write_Output_TopLevelKeysAreInTheDeclaredOrder()
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));
        var keys = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        var expectedOrder = new[]
        {
            "schema_version", "taxonomy_version", "observation_schema_version",
            "extractor_set_version", "classifier_set_version",
            "fact_types", "relations", "mapping_roles", "payload_roles",
            "observation_kinds", "facet_axes", "proof_axes",
        };

        Assert.Equal(expectedOrder, keys);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void Write_Output_EnumeratesEveryFactTypeWithFamilyNameAndIdentityComponents()
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));
        var factTypes = document.RootElement.GetProperty("fact_types").EnumerateArray().ToArray();

        Assert.Equal(Tables.FactTypes.Length, factTypes.Length);

        var solution = factTypes.Single(element => element.GetProperty("name").GetString() == "Solution");
        Assert.Equal("Structural", solution.GetProperty("family").GetString());

        var identityComponents = solution.GetProperty("identity_components").EnumerateArray()
            .Select(value => value.GetString()).ToArray();
        Assert.Equal(new[] { "workspace", "path" }, identityComponents);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void Write_Output_EnumeratesEveryRelationWithItsTriplesAndMinimumEvidenceMethod()
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));
        var relations = document.RootElement.GetProperty("relations").EnumerateArray().ToArray();

        Assert.Equal(Tables.Relations.Length, relations.Length);

        var contains = relations.Single(element => element.GetProperty("kind").GetString() == "Contains");
        Assert.Equal("contains", contains.GetProperty("wire_name").GetString());
        Assert.Equal("Syntactic", contains.GetProperty("minimum_evidence_method").GetString());

        var triples = contains.GetProperty("triples").EnumerateArray()
            .Select(triple => (
                Source: triple.GetProperty("source_fact_type").GetString(),
                Target: triple.GetProperty("target_fact_type").GetString()))
            .ToArray();
        Assert.Contains(("Solution", "Project"), triples);
        Assert.Contains(("Document", "Symbol"), triples);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void Write_Output_EnumeratesEveryObservationKindWithWireNameAndTier()
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));
        var kinds = document.RootElement.GetProperty("observation_kinds").EnumerateArray().ToArray();

        Assert.Equal(Tables.ObservationKinds.Length, kinds.Length);

        var invocation = kinds.Single(element => element.GetProperty("kind").GetString() == "Invocation");
        Assert.Equal("invocation", invocation.GetProperty("wire_name").GetString());
        Assert.Equal("AlwaysWhenBindable", invocation.GetProperty("tier").GetString());
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void Write_Output_EnumeratesEveryFacetAxisWithItsClosedValues()
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));
        var axes = document.RootElement.GetProperty("facet_axes").EnumerateArray().ToArray();

        Assert.Equal(Tables.FacetAxes.Length, axes.Length);

        var boundaryProtocol = axes.Single(element => element.GetProperty("name").GetString() == "boundary-protocol");
        var expectedValues = Tables.FacetAxes.Single(axis => axis.Name == "boundary-protocol").Values.ToArray();
        var actualValues = boundaryProtocol.GetProperty("values").EnumerateArray().Select(value => value.GetString()).ToArray();

        Assert.Equal(expectedValues, actualValues);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void Write_Output_EnumeratesEveryProofAxisWithItsClosedValues()
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));
        var axes = document.RootElement.GetProperty("proof_axes").EnumerateArray().ToArray();

        Assert.Equal(Tables.ProofAxes.Length, axes.Length);

        var evidenceMethod = axes.Single(element => element.GetProperty("name").GetString() == "evidence-method");
        var actualValues = evidenceMethod.GetProperty("values").EnumerateArray().Select(value => value.GetString()).ToArray();

        Assert.Equal(new[] { "semantic", "syntactic", "configured" }, actualValues);
    }

    [Fact]
    [Trait("Requirement", "TAX-84")]
    public void Write_Output_EnumeratesMappingRolesPayloadRolesAndVersionAxes()
    {
        using var document = JsonDocument.Parse(TaxonomyRegistryWriter.Write(Tables));

        var mappingRoles = document.RootElement.GetProperty("mapping_roles").EnumerateArray().Select(v => v.GetString()).ToArray();
        var payloadRoles = document.RootElement.GetProperty("payload_roles").EnumerateArray().Select(v => v.GetString()).ToArray();

        Assert.Equal(Tables.MappingRoles.ToArray(), mappingRoles);
        Assert.Equal(Tables.PayloadRoles.ToArray(), payloadRoles);

        Assert.Equal(Tables.Versions.SchemaVersion, document.RootElement.GetProperty("schema_version").GetInt32());
        Assert.Equal(Tables.Versions.TaxonomyVersion, document.RootElement.GetProperty("taxonomy_version").GetInt32());
        Assert.Equal(Tables.Versions.ObservationSchemaVersion, document.RootElement.GetProperty("observation_schema_version").GetInt32());
        Assert.Equal(Tables.Versions.ExtractorSetVersion, document.RootElement.GetProperty("extractor_set_version").GetInt32());
        Assert.Equal(Tables.Versions.ClassifierSetVersion, document.RootElement.GetProperty("classifier_set_version").GetInt32());
    }

    [Fact]
    [Trait("Requirement", "TAX-87")]
    public void TaxonomyRegistryWriter_IsDeclaredInTheTestAssembly_NotInCsharp2MdDomain()
    {
        Assert.Equal("Csharp2Md.Domain.Tests", typeof(TaxonomyRegistryWriter).Assembly.GetName().Name);
        Assert.NotEqual(typeof(TaxonomyTables).Assembly, typeof(TaxonomyRegistryWriter).Assembly);
    }

    [Fact]
    [Trait("Requirement", "TAX-87")]
    public void Csharp2MdDomain_DeclaresNoReferenceToSystemTextJson()
    {
        var reference = typeof(TaxonomyTables).Assembly.GetReferencedAssemblies()
            .FirstOrDefault(a => a.Name is not null && a.Name.StartsWith("System.Text.Json", StringComparison.Ordinal));

        Assert.True(reference is null, $"Csharp2Md.Domain must not reference System.Text.Json but references '{reference?.Name}'.");
    }
}
