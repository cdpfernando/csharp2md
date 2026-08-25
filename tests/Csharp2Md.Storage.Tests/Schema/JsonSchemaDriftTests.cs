using System.Text.Json;
using Csharp2Md.Domain.Registry;

namespace Csharp2Md.Storage.Tests.Schema;

public sealed class JsonSchemaDriftTests
{
    [Fact]
    [Trait("Requirement", "STOR-01")]
    public void CommittedFactSchemas_CoverEveryRegisteredFactType()
    {
        var factTypes = TaxonomyTables.Default.FactTypes;
        Assert.Equal(17, factTypes.Length);

        foreach (var factType in factTypes)
        {
            var relativePath = JsonSchemaEmitter.FactSchemaPath(factType.Name);
            Assert.True(
                File.Exists(Path.Combine(StorageTestPaths.RepoRoot, relativePath)),
                $"Missing JSON Schema for registered fact type '{factType.Name}' at '{relativePath}'.");
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-02")]
    public void CommittedObservationSchemas_CoverEveryRegisteredWireName()
    {
        var kinds = TaxonomyTables.Default.ObservationKinds;
        Assert.Equal(10, kinds.Length);

        foreach (var kind in kinds)
        {
            var relativePath = JsonSchemaEmitter.ObservationSchemaPath(kind.WireName);
            Assert.True(
                File.Exists(Path.Combine(StorageTestPaths.RepoRoot, relativePath)),
                $"Missing JSON Schema for observation kind '{kind.WireName}' at '{relativePath}'.");
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-03")]
    public void CommittedRelationSchemas_CoverTheFourRelationRecords()
    {
        foreach (var record in JsonSchemaEmitter.RelationRecordNames)
        {
            var relativePath = JsonSchemaEmitter.RelationSchemaPath(record);
            Assert.True(
                File.Exists(Path.Combine(StorageTestPaths.RepoRoot, relativePath)),
                $"Missing JSON Schema for relation record '{record}' at '{relativePath}'.");
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-04")]
    public void CommittedEnvelopeSchemas_CoverTheSixEnvelopes()
    {
        foreach (var envelope in JsonSchemaEmitter.EnvelopeNames)
        {
            var relativePath = JsonSchemaEmitter.EnvelopeSchemaPath(envelope);
            Assert.True(
                File.Exists(Path.Combine(StorageTestPaths.RepoRoot, relativePath)),
                $"Missing JSON Schema for envelope '{envelope}' at '{relativePath}'.");
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-07")]
    public void EveryCommittedSchema_LivesUnderContractsAndDeclaresSchemaVersion1()
    {
        foreach (var (relativePath, _) in JsonSchemaEmitter.EmitAll())
        {
            Assert.StartsWith("contracts/", relativePath.Replace('\\', '/'), StringComparison.Ordinal);

            var committedPath = Path.Combine(StorageTestPaths.RepoRoot, relativePath);
            using var document = JsonDocument.Parse(File.ReadAllText(committedPath));

            Assert.True(
                document.RootElement.TryGetProperty("schema_version", out var version),
                $"'{relativePath}' does not declare schema_version.");
            Assert.Equal(1, version.GetInt32());
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-08")]
    public void CommittedSchemas_MatchFreshEmission_ByteForByte()
    {
        foreach (var (relativePath, emitted) in JsonSchemaEmitter.EmitAll())
        {
            var committed = File.ReadAllBytes(Path.Combine(StorageTestPaths.RepoRoot, relativePath));
            Assert.True(
                committed.AsSpan().SequenceEqual(emitted),
                JsonSchemaEmitter.DescribeMismatch(relativePath, emitted, committed));
        }
    }

    [Fact]
    [Trait("Requirement", "STOR-08")]
    public void DriftGate_NamesTheDifferingFile()
    {
        var (relativePath, emitted) = JsonSchemaEmitter.EmitAll()[0];
        var tampered = emitted.ToArray();
        tampered[^2] ^= 0x01;

        var description = JsonSchemaEmitter.DescribeMismatch(relativePath, emitted, tampered);

        Assert.Contains(relativePath.Replace('\\', '/'), description, StringComparison.Ordinal);
        Assert.False(emitted.AsSpan().SequenceEqual(tampered));
    }
}
