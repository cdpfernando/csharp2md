using System.Text;
using Csharp2Md.Analysis;
using Csharp2Md.Analysis.Storage;
using Csharp2Md.Domain.Identity;
using Csharp2Md.Storage;
using Csharp2Md.Storage.Validation;
using Csharp2Md.Storage.Wire;

namespace Csharp2Md.Storage.Tests.Validation;

public sealed class BatchValidatorTests
{
    [Fact]
    [Trait("Requirement", "GCPC-112")]
    public void Certify_EveryRequiredSolutionPublishedCompatibleAndCertifiable_ReturnsCertified()
    {
        var batch = View(Record("Orders.slnx"), Record("Payments.slnx"));
        var provenance = Provenance();
        var solutions = ImmutableArray.Create(
            Status(batch.Solutions[0], "passed", provenance),
            Status(batch.Solutions[1], "degraded", provenance));

        var result = BatchValidator.Certify(batch, solutions);

        Assert.True(result.Certified);
        Assert.Null(result.Reason);
    }

    [Fact]
    [Trait("Requirement", "GCPC-112")]
    public void Certify_NoSolutions_ReturnsCertifiedVacuously()
    {
        var batch = new BatchView([], []);

        var result = BatchValidator.Certify(batch, []);

        Assert.True(result.Certified);
        Assert.Null(result.Reason);
    }

    [Fact]
    [Trait("Requirement", "GCPC-113")]
    public void Certify_AnUnpublishedRequiredSolution_ReturnsUncertifiedNamingTheReason()
    {
        var published = Record("Orders.slnx");
        var unpublished = Record("Payments.slnx", PublicationStatus.Unpublished);
        var batch = View(published, unpublished);
        var solutions = ImmutableArray.Create(
            Status(published, "passed", Provenance()),
            Status(unpublished, null, null));

        var result = BatchValidator.Certify(batch, solutions);

        Assert.False(result.Certified);
        // GCPC-113/GCPC-114: an unpublished solution is caught by the pre-existing
        // BatchView.Complete/IncompleteScopeReason shape before Certify's own two additional
        // conditions are even evaluated -- Certify defers to that reason verbatim, it does not
        // re-derive or re-word it.
        Assert.Equal("solution-unpublished", result.Reason);
        Assert.Equal(batch.IncompleteScopeReason, result.Reason);
    }

    [Fact]
    [Trait("Requirement", "GCPC-113")]
    public void Certify_AProvenanceIncompatibleSolution_ReturnsUncertifiedNamingTheOffendingSolution()
    {
        var orders = Record("Orders.slnx");
        var payments = Record("Payments.slnx");
        var batch = View(orders, payments);
        var solutions = ImmutableArray.Create(
            Status(orders, "passed", Provenance(schemaVersion: 1)),
            Status(payments, "passed", Provenance(schemaVersion: 2)));

        var result = BatchValidator.Certify(batch, solutions);

        Assert.False(result.Certified);
        Assert.Equal($"provenance-incompatible:{payments.Identity.Value}", result.Reason);
    }

    [Fact]
    [Trait("Requirement", "GCPC-113")]
    public void Certify_ASolutionWhoseOwnRunCertificationFailed_ReturnsUncertifiedNamingTheOffendingSolution()
    {
        var orders = Record("Orders.slnx");
        var payments = Record("Payments.slnx");
        var batch = View(orders, payments);
        var provenance = Provenance();
        var solutions = ImmutableArray.Create(
            Status(orders, "passed", provenance),
            Status(payments, "failed", provenance));

        var result = BatchValidator.Certify(batch, solutions);

        Assert.False(result.Certified);
        Assert.Equal($"solution-not-certifiable:{payments.Identity.Value}", result.Reason);
    }

    private static BatchSolutionCertificationStatus Status(
        BatchSolutionRecord record, string? runCertificationStatus, ProvenanceDto? provenance) =>
        new(record.Identity.Value, runCertificationStatus, provenance);

    private static ProvenanceDto Provenance(int schemaVersion = 2) =>
        new(
            GeneratorVersion: "1.0.0.0",
            BuildIdentity: "00000000-0000-0000-0000-000000000000",
            SchemaVersion: schemaVersion,
            TaxonomyVersion: 2,
            ObservationSchemaVersion: 1,
            ExtractorSetVersion: 2,
            ClassifierSetVersion: 2,
            ArtifactCeilingBytes: 32768,
            TokenEstimatorId: "bytes-over-four",
            DocumentPolicyVersion: "supported-document-policy/1",
            AllowlistDigest: ProvenanceDto.EmptyAllowlistDigest);

    [Fact]
    [Trait("Requirement", "MSC-17")]
    [Trait("Requirement", "MSC-15")]
    public void Validate_EntryCitingIdentityAbsentFromTheBatch_RejectsWithBatchComposition()
    {
        var batch = View(Record("Orders.slnx"), Record("Payments.slnx"));
        var foreign = "solution:workspace=name=default,path=Catalog.slnx";
        var fragments = RelationFragments(
            Source(batch.Solutions[0], "facts/architecture.json", 0),
            Target(foreign, "facts/architecture.json", 0));

        var exception = Assert.Throws<PublicationRejectedException>(
            () => BatchValidator.Validate(batch, fragments));

        Assert.Equal("batch-composition", exception.Gate);
        Assert.Contains(foreign, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-17")]
    public void Validate_EntryCitingArtifactKeyAbsentFromTheContribution_RejectsWithBatchComposition()
    {
        const string missing = "facts/absent.json";
        var orders = Record("Orders.slnx");
        var payments = Record("Payments.slnx");
        var batch = View(orders, payments);
        var fragments = RelationFragments(
            Source(orders, missing, 0),
            Target(payments, "facts/architecture.json", 0));

        var exception = Assert.Throws<PublicationRejectedException>(
            () => BatchValidator.Validate(batch, fragments));

        Assert.Equal("batch-composition", exception.Gate);
        Assert.Contains(missing, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-17")]
    public void Validate_EntryCitingOrdinalAbsentFromTheContribution_RejectsWithBatchComposition()
    {
        const int missingOrdinal = 9;
        var orders = Record("Orders.slnx");
        var payments = Record("Payments.slnx");
        var batch = View(orders, payments);
        var fragments = RelationFragments(
            Source(orders, "facts/architecture.json", 0),
            Target(payments, "facts/architecture.json", missingOrdinal));

        var exception = Assert.Throws<PublicationRejectedException>(
            () => BatchValidator.Validate(batch, fragments));

        Assert.Equal("batch-composition", exception.Gate);
        Assert.Contains(missingOrdinal.ToString(), exception.Detail, StringComparison.Ordinal);
        Assert.Contains("facts/architecture.json", exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-17")]
    public void Validate_EntryPairingASolutionWithItself_RejectsWithBatchComposition()
    {
        var orders = Record("Orders.slnx");
        var batch = View(orders, Record("Payments.slnx"));
        var fragments = RelationFragments(
            Source(orders, "facts/architecture.json", 0),
            Target(orders.Identity.Value, "facts/architecture.json", 1));

        var exception = Assert.Throws<PublicationRejectedException>(
            () => BatchValidator.Validate(batch, fragments));

        Assert.Equal("batch-composition", exception.Gate);
        Assert.Contains(orders.Identity.Value, exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-15")]
    public void Validate_PublishedEmptyArtifact_RejectsWithBatchComposition()
    {
        var batch = View(Record("Orders.slnx"), Record("Payments.slnx"));
        ImmutableArray<StagedFragment> fragments =
        [
            new StagedFragment(
                ArtifactRole.Payload,
                "composition/cross-solution-relations.json",
                Encoding.UTF8.GetBytes("[]").ToImmutableArray()),
        ];

        var exception = Assert.Throws<PublicationRejectedException>(
            () => BatchValidator.Validate(batch, fragments));

        Assert.Equal("batch-composition", exception.Gate);
        Assert.Contains("composition/cross-solution-relations.json", exception.Detail, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "MSC-17")]
    public void Validate_ResolvableLocators_DoesNotThrow()
    {
        var orders = Record("Orders.slnx");
        var payments = Record("Payments.slnx");
        var batch = View(orders, payments);

        BatchValidator.Validate(
            batch,
            RelationFragments(
                Source(orders, "facts/architecture.json", 0),
                Target(payments, "facts/architecture.json", 0)));
    }

    private static BatchView View(params BatchSolutionRecord[] records) =>
        new(
            [.. records],
            [
                Contribution(records[0], ordinals: [0, 1]),
                Contribution(records[1], ordinals: [0]),
            ]);

    private static BatchSolutionRecord Record(string fileName, PublicationStatus status = PublicationStatus.Committed) =>
        new(
            SolutionId.Create(WorkspaceIdentity.Create("default"), fileName),
            fileName,
            status,
            null);

    private static SolutionContribution Contribution(BatchSolutionRecord record, int[] ordinals)
    {
        var operations = ordinals
            .Select(ordinal => new ContributedBoundaryOperation(
                "fact-" + record.SolutionFileName + "-" + ordinal,
                "BoundaryOperation",
                "outbound",
                "messaging",
                null,
                null,
                null,
                "topic",
                "facts/architecture.json",
                ordinal))
            .ToImmutableArray();
        return new SolutionContribution(
            record.Identity.Value,
            record.SolutionFileName,
            "s-test",
            operations,
            [],
            [],
            [],
            []);
    }

    private static (string Identity, string ArtifactKey, int Ordinal) Source(
        BatchSolutionRecord record,
        string artifactKey,
        int ordinal) =>
        (record.Identity.Value, artifactKey, ordinal);

    private static (string Identity, string ArtifactKey, int Ordinal) Target(
        BatchSolutionRecord record,
        string artifactKey,
        int ordinal) =>
        (record.Identity.Value, artifactKey, ordinal);

    private static (string Identity, string ArtifactKey, int Ordinal) Target(
        string identity,
        string artifactKey,
        int ordinal) =>
        (identity, artifactKey, ordinal);

    private static ImmutableArray<StagedFragment> RelationFragments(
        (string Identity, string ArtifactKey, int Ordinal) source,
        (string Identity, string ArtifactKey, int Ordinal) target)
    {
        var json = $$"""
            [{
              "kind": "targets",
              "source_fact_id": "fact-source",
              "source_solution_identity": "{{source.Identity}}",
              "source_artifact_key": "{{source.ArtifactKey}}",
              "source_ordinal": {{source.Ordinal}},
              "target_fact_id": "fact-target",
              "target_solution_identity": "{{target.Identity}}",
              "target_artifact_key": "{{target.ArtifactKey}}",
              "target_ordinal": {{target.Ordinal}},
              "matched_key": "topic"
            }]
            """;
        return
        [
            new StagedFragment(
                ArtifactRole.Payload,
                "composition/cross-solution-relations.json",
                Encoding.UTF8.GetBytes(json).ToImmutableArray()),
        ];
    }
}
